// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

        DatabaseMigrator.Migrate(contextFactory, databasePath);

        using var context = contextFactory.CreateDbContext();
        context.Database.GetPendingMigrations().Should().BeEmpty();
        CountMigrationHistoryTables(backupPath).Should().Be(0);
    }

    [Fact]
    public void Migrate_WhenBackupCannotBeReplaced_DoesNotApplyMigrations()
    {
        using var temporaryDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(temporaryDirectory.Path);
        var databasePath = Path.Combine(temporaryDirectory.Path, "gbcnet.sqlite");
        CreateEmptyDatabase(databasePath);
        Directory.CreateDirectory(databasePath + ".bak");
        var contextFactory = new TestDbContextFactory(databasePath);

        FluentActions
            .Invoking(() => DatabaseMigrator.Migrate(contextFactory, databasePath))
            .Should()
            .Throw<IOException>();

        CountMigrationHistoryTables(databasePath).Should().Be(0);
    }

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

    private sealed class TestDbContextFactory(string databasePath)
        : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options =
            new DbContextOptionsBuilder<GbcNetDbContext>()
                .UseSqlite(SqliteDbContextOptions.CreateConnectionString(databasePath))
                .Options;

        public GbcNetDbContext CreateDbContext() => new(_options);
    }
}
