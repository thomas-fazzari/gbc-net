// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Database;
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
    public async Task Migrate_ConcurrentCallsForSameDatabase_SerializesMigration()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        CreateEmptyDatabase(databasePath);
        using var firstContextCreated = new ManualResetEventSlim(initialState: false);
        using var releaseFirstContext = new ManualResetEventSlim(initialState: false);
        using var secondContextCreated = new ManualResetEventSlim(initialState: false);
        var contextCreationCount = 0;
        var cancellationToken = TestContext.Current.CancellationToken;
        var contextFactory = new TestDbContextFactory(
            databasePath,
            () =>
            {
                if (Interlocked.Increment(ref contextCreationCount) == 1)
                {
                    firstContextCreated.Set();
                    releaseFirstContext.Wait(cancellationToken);
                    return;
                }

                secondContextCreated.Set();
            }
        );

        var firstMigration = StartMigration(contextFactory, databasePath);
        Task? secondMigration = null;

        try
        {
            firstContextCreated.Wait(cancellationToken);

            secondMigration = StartMigration(contextFactory, databasePath);
            secondContextCreated
                .Wait(TimeSpan.FromMilliseconds(100), cancellationToken)
                .Should()
                .BeFalse();
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
            () =>
            {
                firstContextCreated.Set();
                releaseContexts.Wait(cancellationToken);
            }
        );
        var secondContextFactory = new TestDbContextFactory(
            secondDatabasePath,
            () =>
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
            secondContextCreated.Wait(TimeSpan.FromSeconds(5), cancellationToken).Should().BeTrue();
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
        string databasePath
    ) =>
        Task.Factory.StartNew(
            () => DatabaseMigrator.Migrate(contextFactory, databasePath, NullLogger.Instance),
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

    private sealed class TestDbContextFactory(string databasePath, Action? beforeCreate = null)
        : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options = SqliteDbContextOptions
            .Configure(new DbContextOptionsBuilder<GbcNetDbContext>(), databasePath)
            .Options;

        public GbcNetDbContext CreateDbContext()
        {
            beforeCreate?.Invoke();
            return new(_options);
        }
    }
}
