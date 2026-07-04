// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.App.Library.Migrations;
using Microsoft.Data.Sqlite;

namespace GbcNet.App.Library;

internal sealed class LibraryDatabase(string databasePath)
{
    private readonly Lock _migrationLock = new();
    private bool _migrated;

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(CreateConnectionString());

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
            connection.Open();
            EnsureMigrated(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void EnsureMigrated(SqliteConnection connection)
    {
        if (_migrated)
        {
            return;
        }

        lock (_migrationLock)
        {
            if (_migrated)
            {
                return;
            }

            Migrate(connection);
            _migrated = true;
        }
    }

    private static void Migrate(SqliteConnection connection)
    {
        var schemaVersion = GetUserVersion(connection);
        if (schemaVersion == DbMigrations.LatestVersion)
        {
            return;
        }

        if (schemaVersion > DbMigrations.LatestVersion)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Unsupported ROM library database schema version {schemaVersion}."
                )
            );
        }

        using var transaction = connection.BeginTransaction();
        foreach (var migration in DbMigrations.All)
        {
            if (migration.Version <= schemaVersion)
            {
                continue;
            }

            migration.Up(connection, transaction);
        }

        transaction.Commit();
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private string CreateConnectionString() =>
        new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
}
