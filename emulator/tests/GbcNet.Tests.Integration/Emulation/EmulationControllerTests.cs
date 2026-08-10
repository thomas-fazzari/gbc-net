// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Platform.Storage;
using ErrorOr;
using GbcNet.App.Cheats;
using GbcNet.App.Database.Entities;
using GbcNet.App.Emulation;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Cheats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Emulation;

public sealed class EmulationControllerTests
{
    [Fact]
    public async Task OpenRomFileAsync_KeepsActiveSessionAndSnapshotWhenNextRomCheatsCannotLoad()
    {
        using var test = new ControllerTestContext();
        var romA = TestRomFactory.Create();
        var romB = TestRomFactory.Create(bytes => bytes[0x0200] = 0x42);
        var expectedCodes = new[]
        {
            new CheatCodeEntry(
                CheatCodeParser.Parse(CheatCodeType.GameGenie, "0A1-B9F"),
                IsEnabled: true,
                "Active Genie"
            ),
        };
        await test.CheatCodes.ReplaceAsync(
            SHA256.HashData(romA),
            expectedCodes,
            TestContext.Current.CancellationToken
        );
        await using (var db = test.DbContextFactory.CreateDbContext())
        {
            db.CheatCodes.Add(
                new StoredCheatCode(
                    Convert.ToHexString(SHA256.HashData(romB)),
                    CheatCodeType.GameGenie,
                    sortOrder: 0,
                    code: "INVALID",
                    isEnabled: true
                )
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var controller = test.CreateController();
        try
        {
            var activeResult = await controller.OpenRomFileAsync(
                TestStorageFile.Create("active.gb", romA)
            );
            activeResult.IsError.Should().BeFalse();
            var active = activeResult.Value;

            var exception = (
                await FluentActions
                    .Awaiting(() =>
                        controller.OpenRomFileAsync(TestStorageFile.Create("broken.gb", romB))
                    )
                    .Should()
                    .ThrowExactlyAsync<InvalidOperationException>()
            ).Which;

            exception.Message.Should().Be("Cheat codes could not be loaded.");
            controller.State.HasSession.Should().BeTrue();
            controller.State.LoadedRom.ToArray().Should().Equal(active.LoadedRom.ToArray());
            controller.State.LoadedRomFileName.Should().Be(active.LoadedRomFileName);
            controller.State.CheatCodes.ToArray().Should().Equal(expectedCodes);

            var updatedCodes = new[]
            {
                new CheatCodeEntry(
                    CheatCodeParser.Parse(CheatCodeType.GameShark, "0134CDC0"),
                    IsEnabled: true,
                    "Live Shark"
                ),
            };
            await controller.SetCheatCodesAsync(
                updatedCodes,
                TestContext.Current.CancellationToken
            );

            controller.State.CheatCodes.ToArray().Should().Equal(updatedCodes);
            (
                await test.CheatCodes.LoadAsync(
                    SHA256.HashData(romA),
                    TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Equal(updatedCodes);
        }
        finally
        {
            await controller.StopAsync();
        }
    }

    [Fact]
    public async Task OpenRomFileAsync_ReturnsValidationErrorAndKeepsActiveSessionForInvalidRom()
    {
        using var test = new ControllerTestContext();
        var controller = test.CreateController();
        try
        {
            var activeResult = await controller.OpenRomFileAsync(
                TestStorageFile.Create("active.gb", TestRomFactory.Create())
            );
            activeResult.IsError.Should().BeFalse();
            controller.TogglePause();
            var activeState = controller.State;

            var invalidResult = await controller.OpenRomFileAsync(
                TestStorageFile.Create("invalid.gb", [])
            );

            invalidResult.IsError.Should().BeTrue();
            var error = invalidResult.Errors.Should().ContainSingle().Which;
            error.Type.Should().Be(ErrorType.Validation);
            error.Code.Should().Be("Rom.RomTooSmall");
            error
                .Description.Should()
                .Be("ROM must contain at least 336 bytes to include the cartridge header.");
            controller.State.HasSession.Should().BeTrue();
            controller.State.Should().Be(activeState);
        }
        finally
        {
            await controller.StopAsync();
        }
    }

    [Fact]
    public async Task SetCheatCodesAsync_KeepsGenericSnapshotWhenPersistenceFails()
    {
        using var test = new ControllerTestContext();
        var rom = TestRomFactory.Create();
        var existingCodes = new[]
        {
            new CheatCodeEntry(
                CheatCodeParser.Parse(CheatCodeType.GameGenie, "0A1-B9F"),
                IsEnabled: true,
                "Existing Genie"
            ),
            new CheatCodeEntry(
                CheatCodeParser.Parse(CheatCodeType.GameShark, "0155CDC0"),
                IsEnabled: false,
                "Disabled Shark"
            ),
        };
        await test.CheatCodes.ReplaceAsync(
            SHA256.HashData(rom),
            existingCodes,
            TestContext.Current.CancellationToken
        );

        var controller = test.CreateController(test.CreateFailingCheatCodeService());
        try
        {
            (await controller.OpenRomFileAsync(TestStorageFile.Create("game.gb", rom)))
                .IsError.Should()
                .BeFalse();

            var exception = (
                await FluentActions
                    .Awaiting(() =>
                        controller.SetCheatCodesAsync(
                            [
                                new CheatCodeEntry(
                                    CheatCodeParser.Parse(CheatCodeType.GameShark, "01AACDC0"),
                                    IsEnabled: true,
                                    "Replacement Shark"
                                ),
                            ],
                            TestContext.Current.CancellationToken
                        )
                    )
                    .Should()
                    .ThrowExactlyAsync<InvalidOperationException>()
            ).Which;

            exception.Message.Should().Be("Cheat codes could not be saved.");
            controller.State.CheatCodes.ToArray().Should().Equal(existingCodes);
        }
        finally
        {
            await controller.StopAsync();
        }
    }

    [Fact]
    public async Task ResetAsync_ReusesActiveGameSharkSnapshotInsteadOfReloadingDatabase()
    {
        using var test = new ControllerTestContext();
        var rom = TestRomFactory.Create();
        var initialCodes = new[]
        {
            new CheatCodeEntry(
                CheatCodeParser.Parse(CheatCodeType.GameShark, "0123CDC0"),
                IsEnabled: true,
                "Initial Shark"
            ),
        };
        await test.CheatCodes.ReplaceAsync(
            SHA256.HashData(rom),
            initialCodes,
            TestContext.Current.CancellationToken
        );
        var controller = test.CreateController();
        try
        {
            (await controller.OpenRomFileAsync(TestStorageFile.Create("game.gb", rom)))
                .IsError.Should()
                .BeFalse();
            controller.State.CheatCodes.ToArray().Should().Equal(initialCodes);

            var changedCodes = new[]
            {
                new CheatCodeEntry(
                    CheatCodeParser.Parse(CheatCodeType.GameShark, "0145CDC0"),
                    IsEnabled: true,
                    "Changed Shark"
                ),
            };
            await test.CheatCodes.ReplaceAsync(
                SHA256.HashData(rom),
                changedCodes,
                TestContext.Current.CancellationToken
            );

            await controller.ResetAsync();

            controller.State.CheatCodes.ToArray().Should().Equal(initialCodes);
        }
        finally
        {
            await controller.StopAsync();
        }
    }

    private sealed class ControllerTestContext : IDisposable
    {
        private readonly TestDirectories.TemporaryDirectory _temporaryDirectory =
            TestDirectories.CreateTemporaryDirectory();

        public ControllerTestContext()
        {
            Directory.CreateDirectory(DirectoryPath);
            DbContextFactory = new TestDbContextFactory(DatabasePath);
            using var db = DbContextFactory.CreateDbContext();
            db.Database.Migrate();
            CheatCodes = new CheatCodeService(DbContextFactory);
        }

        private string DirectoryPath => _temporaryDirectory.Path;

        private string DatabasePath => Path.Combine(DirectoryPath, "gbcnet.sqlite");

        public TestDbContextFactory DbContextFactory { get; }

        public CheatCodeService CheatCodes { get; }

        private TestAudioOutput AudioOutput { get; } = new();

        public CheatCodeService CreateFailingCheatCodeService() =>
            new(new TestDbContextFactory(DatabasePath, FailingSaveChangesInterceptor.Instance));

        public EmulationController CreateController(CheatCodeService? cheatCodes = null) =>
            new(
                new BootRomOptions(),
                AudioOutput,
                new CartridgeBatterySaveFileService(DirectoryPath),
                new SaveStateFileService(DirectoryPath, NullLogger<SaveStateFileService>.Instance),
                cheatCodes ?? CheatCodes,
                static _ => { },
                static _ => { },
                static _ => { },
                fastForwardEnabled: false,
                EmulationSpeed.Two
            );

        public void Dispose()
        {
            AudioOutput.Dispose();
            _temporaryDirectory.Dispose();
        }
    }
}

public class TestStorageFile : DispatchProxy
{
    private static readonly AsyncLocal<StorageFileContent?> _pendingContent = new();

    private readonly byte[] _data;
    private readonly string _name;

    public TestStorageFile()
    {
        var content =
            _pendingContent.Value
            ?? throw new InvalidOperationException(
                "Test storage file content was not initialized."
            );
        _name = content.Name;
        _data = content.Data;
    }

    public static IStorageFile Create(string name, byte[] data)
    {
        _pendingContent.Value = new StorageFileContent(name, data);
        try
        {
            return Create<IStorageFile, TestStorageFile>();
        }
        finally
        {
            _pendingContent.Value = null;
        }
    }

    protected override object Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name switch
        {
            "get_Name" => _name,
            nameof(IStorageFile.OpenReadAsync) => Task.FromResult<Stream>(
                new MemoryStream(_data, writable: false)
            ),
            _ => throw new NotSupportedException(targetMethod?.Name),
        };

    private sealed record StorageFileContent(string Name, byte[] Data);
}
