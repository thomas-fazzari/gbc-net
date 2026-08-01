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
        Assert.True(GameGenieCode.TryParse("0A1-B9F", out var codeA));
        var expectedCodes = new[] { new GameGenieCodeEntry(codeA, true) };
        await test.GameGenieCodes.ReplaceAsync(
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

            Assert.Equal("Game Genie codes could not be loaded.", exception.Message);
            Assert.True(controller.State.HasSession);
            Assert.Equal(active.LoadedRom.ToArray(), controller.State.LoadedRom.ToArray());
            Assert.Equal(active.LoadedRomFileName, controller.State.LoadedRomFileName);
            Assert.Equal(expectedCodes, controller.State.GameGenieCodes.ToArray());
            Assert.True(GameGenieCode.TryParse("05D-49C-E62", out var updatedCode));
            var updatedCodes = new[] { new GameGenieCodeEntry(updatedCode, true) };
            await controller.SetGameGenieCodesAsync(
                updatedCodes,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(
                updatedCodes,
                await test.GameGenieCodes.LoadAsync(
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
    public async Task SetGameGenieCodesAsync_KeepsSnapshotWhenPersistenceFails()
    {
        using var test = new ControllerTestContext();
        var rom = TestRomFactory.Create();
        Assert.True(GameGenieCode.TryParse("0A1-B9F", out var existingCode));
        Assert.True(GameGenieCode.TryParse("05D-49C-E62", out var replacementCode));
        var existingCodes = new[] { new GameGenieCodeEntry(existingCode, true) };
        await test.GameGenieCodes.ReplaceAsync(
            SHA256.HashData(rom),
            existingCodes,
            TestContext.Current.CancellationToken
        );

        var controller = test.CreateController(test.CreateFailingGameGenieService());
        try
        {
            await controller.OpenRomFileAsync(TestStorageFile.Create("game.gb", rom));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                controller.SetGameGenieCodesAsync(
                    [new GameGenieCodeEntry(replacementCode, true)],
                    TestContext.Current.CancellationToken
                )
            );

            Assert.Equal("Game Genie codes could not be saved.", exception.Message);
            Assert.Equal(existingCodes, controller.State.GameGenieCodes.ToArray());
        }
        finally
        {
            await controller.StopAsync();
        }
    }

    [Fact]
    public async Task ResetAsync_ReusesCurrentCodeSnapshotInsteadOfReloadingDatabase()
    {
        using var test = new ControllerTestContext();
        var rom = TestRomFactory.Create();
        Assert.True(GameGenieCode.TryParse("0A1-B9F", out var initialCode));
        Assert.True(GameGenieCode.TryParse("05D-49C-E62", out var changedCode));
        var initialCodes = new[] { new GameGenieCodeEntry(initialCode, true) };
        await test.GameGenieCodes.ReplaceAsync(
            SHA256.HashData(rom),
            initialCodes,
            TestContext.Current.CancellationToken
        );
        var controller = test.CreateController();
        try
        {
            await controller.OpenRomFileAsync(TestStorageFile.Create("game.gb", rom));
            await test.GameGenieCodes.ReplaceAsync(
                SHA256.HashData(rom),
                [new GameGenieCodeEntry(changedCode, true)],
                TestContext.Current.CancellationToken
            );

            await controller.ResetAsync();

            Assert.Equal(initialCodes, controller.State.GameGenieCodes.ToArray());
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
            GameGenieCodes = new GameGenieService(DbContextFactory);
        }

        private string DirectoryPath => _temporaryDirectory.Path;

        private string DatabasePath => Path.Combine(DirectoryPath, "gbcnet.sqlite");

        public TestDbContextFactory DbContextFactory { get; }

        public GameGenieService GameGenieCodes { get; }

        private TestAudioOutput AudioOutput { get; } = new();

        public GameGenieService CreateFailingGameGenieService() =>
            new(new FailingDbContextFactory(DatabasePath));

        public EmulationController CreateController(GameGenieService? gameGenieCodes = null) =>
            new(
                new BootRomOptions(),
                AudioOutput,
                new CartridgeBatterySaveFileService(DirectoryPath),
                new SaveStateFileService(DirectoryPath, NullLogger<SaveStateFileService>.Instance),
                gameGenieCodes ?? GameGenieCodes,
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
