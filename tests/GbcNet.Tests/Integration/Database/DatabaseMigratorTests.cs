// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Entities;
using GbcNet.App.Infrastructure.Persistence;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cheats;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Database;

public sealed class DatabaseMigratorTests
{
    [Fact]
    public void Migrate_WithExistingDatabase_CreatesUsablePreMigrationBackup()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        var backupPath = databasePath + ".bak";
        CreateEmptyDatabase(databasePath);
        File.WriteAllText(backupPath, "stale backup");
        var contextFactory = new TestDbContextFactory(databasePath);

        DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance);

        using var context = contextFactory.CreateDbContext();
        context.Database.GetPendingMigrations().Should().BeEmpty();
        CountMigrationHistoryTables(backupPath).Should().Be(0);
    }

    [Fact]
    public void Migrate_PathCollationUpgradePreservesRowsAndUniquePathInvariant()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        var romPath = Path.Combine(temporaryDirectory.Path, "GAME.gb");

        var contextFactory = new TestDbContextFactory(databasePath);
        var originalRom = LibraryRom.Create(
            new string('A', 64),
            romPath,
            Path.GetFileName(romPath),
            cartridgeTitle: "ORIGINAL",
            CartridgeHardwareKind.GB,
            noIntroHash: new string('0', 40),
            openedAt: new DateTimeOffset(2026, 8, 10, 22, 0, 0, TimeSpan.Zero)
        );

        using (var context = contextFactory.CreateDbContext())
        {
            var previousMigration = context.Database.GetMigrations().SkipLast(1).Last();
            context.Database.Migrate(previousMigration);
            context.Roms.Add(originalRom);
            context.SaveChanges();
        }

        DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance);

        using (var context = contextFactory.CreateDbContext())
        {
            context.Database.GetPendingMigrations().Should().BeEmpty();
            var savedRom = context.Roms.Should().ContainSingle().Which;
            savedRom.RomHash.Should().Be(originalRom.RomHash);
            savedRom.LastKnownPath.Should().Be(romPath);
            savedRom.CartridgeTitle.Should().Be("ORIGINAL");

            context.Roms.Add(
                LibraryRom.Create(
                    new string('B', 64),
                    romPath,
                    Path.GetFileName(romPath),
                    cartridgeTitle: "DUPLICATE",
                    CartridgeHardwareKind.GB,
                    noIntroHash: new string('1', 40),
                    openedAt: new DateTimeOffset(2026, 8, 10, 22, 1, 0, TimeSpan.Zero)
                )
            );
            var exception = FluentActions
                .Invoking(context.SaveChanges)
                .Should()
                .ThrowExactly<DbUpdateException>()
                .Which;
            exception.InnerException.Should().BeOfType<SqliteException>();
        }

        using var verificationContext = contextFactory.CreateDbContext();
        verificationContext
            .Roms.Should()
            .ContainSingle()
            .Which.RomHash.Should()
            .Be(originalRom.RomHash);
    }

    [Fact]
    public void Migrate_EnumMappingUpgradePreservesStoredCheatCodes()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        var romHash = new string('A', 64);
        var contextFactory = new TestDbContextFactory(databasePath);

        using (var context = contextFactory.CreateDbContext())
        {
            var previousMigration = context.Database.GetMigrations().SkipLast(1).Last();
            context.Database.Migrate(previousMigration);
            context.Database.ExecuteSqlInterpolated(
                $"""
                INSERT INTO cheat_codes (rom_hash, type, sort_order, code, name, is_enabled)
                VALUES ({romHash}, 0, 0, '068-55F-E66', 'Infinite lives', 1);
                """
            );
        }

        DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance);

        using var verificationContext = contextFactory.CreateDbContext();
        var storedCode = verificationContext.CheatCodes.Should().ContainSingle().Which;
        storedCode.RomHash.Should().Be(romHash);
        storedCode.Type.Should().Be(CheatCodeType.GameGenie);
        storedCode.SortOrder.Should().Be(0);
        storedCode.Code.Should().Be("068-55F-E66");
        storedCode.Name.Should().Be("Infinite lives");
        storedCode.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Migrate_ConcurrentCallsForSameDatabase_SerializesMigration()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        CreateEmptyDatabase(databasePath);

        using var firstContextCreated = new ManualResetEventSlim(initialState: false);
        using var secondMigrationStarted = new ManualResetEventSlim(initialState: false);
        using var releaseFirstContext = new ManualResetEventSlim(initialState: false);
        var contextCreationCount = 0;
        var firstContextReleased = 0;
        var cancellationToken = TestContext.Current.CancellationToken;
        var contextFactory = new TestDbContextFactory(
            databasePath,
            beforeCreate: () =>
            {
                if (Interlocked.Increment(ref contextCreationCount) == 1)
                {
                    firstContextCreated.Set();
                    releaseFirstContext.Wait(cancellationToken);
                    Volatile.Write(ref firstContextReleased, 1);
                    return;
                }

                Volatile.Read(ref firstContextReleased).Should().Be(1);
            }
        );

        var firstMigration = StartMigration(contextFactory, databasePath);
        Task? secondMigration = null;

        try
        {
            firstContextCreated.Wait(cancellationToken);

            secondMigration = StartMigration(
                contextFactory,
                databasePath,
                secondMigrationStarted.Set
            );
            secondMigrationStarted.Wait(cancellationToken);
        }
        finally
        {
            releaseFirstContext.Set();
            await firstMigration.WaitAsync(cancellationToken);
            if (secondMigration is not null)
            {
                await secondMigration.WaitAsync(cancellationToken);
            }
        }

        contextCreationCount.Should().Be(2);
        File.Exists(databasePath + ".bak.tmp").Should().BeFalse();
        CountMigrationHistoryTables(databasePath).Should().Be(1);
    }

    [Fact]
    public async Task Migrate_ConcurrentCallsForDifferentDatabases_DoNotShareMigrationLock()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var firstDatabasePath = Path.Combine(temporaryDirectory.Path, "first.sqlite");
        var secondDatabasePath = Path.Combine(temporaryDirectory.Path, "second.sqlite");
        CreateEmptyDatabase(firstDatabasePath);
        CreateEmptyDatabase(secondDatabasePath);

        using var firstContextCreated = new ManualResetEventSlim(initialState: false);
        using var secondContextCreated = new ManualResetEventSlim(initialState: false);
        using var releaseContexts = new ManualResetEventSlim(initialState: false);

        var cancellationToken = TestContext.Current.CancellationToken;
        var firstContextFactory = new TestDbContextFactory(
            firstDatabasePath,
            beforeCreate: () =>
            {
                firstContextCreated.Set();
                releaseContexts.Wait(cancellationToken);
            }
        );
        var secondContextFactory = new TestDbContextFactory(
            secondDatabasePath,
            beforeCreate: () =>
            {
                secondContextCreated.Set();
                releaseContexts.Wait(cancellationToken);
            }
        );

        var firstMigration = StartMigration(firstContextFactory, firstDatabasePath);
        Task? secondMigration = null;

        try
        {
            firstContextCreated.Wait(cancellationToken);

            secondMigration = StartMigration(secondContextFactory, secondDatabasePath);
            secondContextCreated.Wait(cancellationToken);
        }
        finally
        {
            releaseContexts.Set();
            await firstMigration.WaitAsync(cancellationToken);
            if (secondMigration is not null)
            {
                await secondMigration.WaitAsync(cancellationToken);
            }
        }

        secondContextCreated.IsSet.Should().BeTrue();
    }

    [Fact]
    public void Migrate_WhenBackupCannotBeReplaced_ReleasesMigrationLock()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        CreateEmptyDatabase(databasePath);
        Directory.CreateDirectory(databasePath + ".bak");
        var contextFactory = new TestDbContextFactory(databasePath);

        FluentActions
            .Invoking(() =>
                DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance)
            )
            .Should()
            .Throw<IOException>();

        CountMigrationHistoryTables(databasePath).Should().Be(0);

        Directory.Delete(databasePath + ".bak");
        DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance);

        CountMigrationHistoryTables(databasePath).Should().Be(1);
    }

    private static Task StartMigration(
        IDbContextFactory<GbcNetDbContext> contextFactory,
        string databasePath,
        Action? beforeMigration = null
    ) =>
        Task.Factory.StartNew(
            () =>
            {
                beforeMigration?.Invoke();
                DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

    private static void CreateEmptyDatabase(string databasePath)
    {
        using var connection = new SqliteConnection(
            SqliteDbContextOptions.CreateConnectionString(databasePath)
        );
        connection.Open();
    }

    private static long CountMigrationHistoryTables(string databasePath)
    {
        using var connection = new SqliteConnection(
            SqliteDbContextOptions.CreateConnectionString(databasePath)
        );
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';";
        return (long)(command.ExecuteScalar() ?? 0L);
    }
}
