// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GbcNet.App.Database;

internal static class DatabaseMigrator
{
    internal static void Migrate(
        IDbContextFactory<GbcNetDbContext> contextFactory,
        string databaseFilePath
    )
    {
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
                    SqliteDbContextOptions.CreateConnectionString(temporaryBackupPath)
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
