// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Data.Common;
using GbcNet.App.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GbcNet.App.Infrastructure.Persistence;

internal static class SqliteDbContextOptions
{
    internal const string OrdinalIgnoreCaseCollation = "GBCNET_ORDINAL_IGNORE_CASE";
    internal const string FileSystemPathCollation = "GBCNET_FILE_SYSTEM_PATH";

    internal static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string databaseFilePath
    ) =>
        options
            .UseSqlite(CreateConnectionString(databaseFilePath))
            .AddInterceptors(SqliteCollationInterceptor.Instance);

    internal static DbContextOptionsBuilder<TContext> Configure<TContext>(
        DbContextOptionsBuilder<TContext> options,
        string databaseFilePath
    )
        where TContext : DbContext
    {
        Configure((DbContextOptionsBuilder)options, databaseFilePath);
        return options;
    }

    internal static string CreateConnectionString(string databaseFilePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            ForeignKeys = true,
        }.ToString();

    private sealed class SqliteCollationInterceptor : DbConnectionInterceptor
    {
        internal static SqliteCollationInterceptor Instance { get; } = new();

        public override void ConnectionOpened(
            DbConnection connection,
            ConnectionEndEventData eventData
        ) => RegisterCollation(connection);

        public override Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            RegisterCollation(connection);
            return Task.CompletedTask;
        }

        private static void RegisterCollation(DbConnection connection)
        {
            var sqliteConnection = (SqliteConnection)connection;
            var fileSystemPathComparer =
                FileUtils.GetFileSystemPathComparison() == StringComparison.OrdinalIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
            sqliteConnection.CreateCollation(
                OrdinalIgnoreCaseCollation,
                StringComparer.OrdinalIgnoreCase.Compare
            );
            sqliteConnection.CreateCollation(
                FileSystemPathCollation,
                fileSystemPathComparer.Compare
            );
        }
    }
}
