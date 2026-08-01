// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Platform.Storage;
using GbcNet.App.Cheats;
using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.App.Emulation;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Cheats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
                ParseCode(CheatCodeType.GameGenie, "0A1-B9F"),
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
            var active = await controller.OpenRomFileAsync(
                TestStorageFile.Create("active.gb", romA)
            );

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                controller.OpenRomFileAsync(TestStorageFile.Create("broken.gb", romB))
            );

            Assert.Equal("Cheat codes could not be loaded.", exception.Message);
            Assert.True(controller.State.HasSession);
            Assert.Equal(active.LoadedRom.ToArray(), controller.State.LoadedRom.ToArray());
            Assert.Equal(active.LoadedRomFileName, controller.State.LoadedRomFileName);
            Assert.Equal(expectedCodes, controller.State.CheatCodes.ToArray());

            var updatedCodes = new[]
            {
                new CheatCodeEntry(
                    ParseCode(CheatCodeType.GameShark, "0134CDC0"),
                    IsEnabled: true,
                    "Live Shark"
                ),
            };
            await controller.SetCheatCodesAsync(
                updatedCodes,
                TestContext.Current.CancellationToken
            );

            Assert.Equal(updatedCodes, controller.State.CheatCodes.ToArray());
            Assert.Equal(
                updatedCodes,
                await test.CheatCodes.LoadAsync(
                    SHA256.HashData(romA),
                    TestContext.Current.CancellationToken
                )
            );
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
                ParseCode(CheatCodeType.GameGenie, "0A1-B9F"),
                IsEnabled: true,
                "Existing Genie"
            ),
            new CheatCodeEntry(
                ParseCode(CheatCodeType.GameShark, "0155CDC0"),
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
            await controller.OpenRomFileAsync(TestStorageFile.Create("game.gb", rom));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                controller.SetCheatCodesAsync(
                    [
                        new CheatCodeEntry(
                            ParseCode(CheatCodeType.GameShark, "01AACDC0"),
                            IsEnabled: true,
                            "Replacement Shark"
                        ),
                    ],
                    TestContext.Current.CancellationToken
                )
            );

            Assert.Equal("Cheat codes could not be saved.", exception.Message);
            Assert.Equal(existingCodes, controller.State.CheatCodes.ToArray());
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
                ParseCode(CheatCodeType.GameShark, "0123CDC0"),
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
            await controller.OpenRomFileAsync(TestStorageFile.Create("game.gb", rom));
            Assert.Equal(initialCodes, controller.State.CheatCodes.ToArray());

            var changedCodes = new[]
            {
                new CheatCodeEntry(
                    ParseCode(CheatCodeType.GameShark, "0145CDC0"),
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

            Assert.Equal(initialCodes, controller.State.CheatCodes.ToArray());
        }
        finally
        {
            await controller.StopAsync();
        }
    }

    private static CheatCode ParseCode(CheatCodeType type, string text)
    {
        Assert.True(CheatCode.TryParse(type, text, out var code));
        return code;
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
            new(new FailingDbContextFactory(DatabasePath));

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

    private sealed class TestDbContextFactory(string databasePath)
        : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options =
            new DbContextOptionsBuilder<GbcNetDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

        public GbcNetDbContext CreateDbContext() => new(_options);
    }

    private sealed class FailingDbContextFactory(string databasePath)
        : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options =
            new DbContextOptionsBuilder<GbcNetDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .AddInterceptors(FailingSaveChangesInterceptor.Instance)
                .Options;

        public GbcNetDbContext CreateDbContext() => new(_options);
    }

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public static FailingSaveChangesInterceptor Instance { get; } = new();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        ) => throw new DbUpdateException("Synthetic save failure.");
    }
}

public class TestStorageFile : DispatchProxy
{
    private byte[] _data = [];
    private string _name = string.Empty;

    public static IStorageFile Create(string name, byte[] data)
    {
        var storageFile = Create<IStorageFile, TestStorageFile>();
        var proxy = (TestStorageFile)(object)storageFile;
        proxy._name = name;
        proxy._data = data;
        return storageFile;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name switch
        {
            "get_Name" => _name,
            nameof(IStorageFile.OpenReadAsync) => Task.FromResult<Stream>(
                new MemoryStream(_data, writable: false)
            ),
            _ => throw new NotSupportedException(targetMethod?.Name),
        };
}
