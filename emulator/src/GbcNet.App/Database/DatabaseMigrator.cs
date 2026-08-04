// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Database;

internal static class DatabaseMigrator
{
    internal static void Migrate(
        IDbContextFactory<GbcNetDbContext> contextFactory,
        string databaseFilePath,
        ILogger logger
    )
    {
        using var migrationMutex = new Mutex(
            initiallyOwned: false,
            name: GetMigrationMutexName(databaseFilePath)
        );
        var lockAcquired = false;

        try
        {
            try
            {
                migrationMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                DatabaseLog.MigrationLockAbandoned(logger);
            }

            lockAcquired = true;
            Directory.CreateDirectory(Path.GetDirectoryName(databaseFilePath) ?? ".");
            var databaseExisted = File.Exists(databaseFilePath);

            using var context = contextFactory.CreateDbContext();
            if (!context.Database.GetPendingMigrations().Any())
            {
                return;
            }

            if (databaseExisted)
            {
                BackupDatabase(
                    context.Database.GetConnectionString()
                        ?? throw new InvalidOperationException(
                            "Database connection string is not configured."
                        ),
                    databaseFilePath
                );
            }

            context.Database.Migrate();
        }
        finally
        {
            if (lockAcquired)
            {
                migrationMutex.ReleaseMutex();
            }
        }
    }

    private static string GetMigrationMutexName(string databaseFilePath) =>
        "GbcNet.DatabaseMigration."
        + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(databaseFilePath)))
        );

    private static void BackupDatabase(string sourceConnectionString, string databaseFilePath)
    {
        var backupPath = databaseFilePath + ".bak";
        var temporaryBackupPath = backupPath + ".tmp";
        File.Delete(temporaryBackupPath);

        try
        {
            using (var source = new SqliteConnection(sourceConnectionString))
            using (
                var destination = new SqliteConnection(
                    new SqliteConnectionStringBuilder(
                        SqliteDbContextOptions.CreateConnectionString(temporaryBackupPath)
                    )
                    {
                        Pooling = false,
                    }.ToString()
                )
            )
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            File.Move(temporaryBackupPath, backupPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryBackupPath);
        }
    }
}

internal static partial class DatabaseLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The database migration lock was abandoned by another process."
    )]
    internal static partial void MigrationLockAbandoned(ILogger logger);
}
