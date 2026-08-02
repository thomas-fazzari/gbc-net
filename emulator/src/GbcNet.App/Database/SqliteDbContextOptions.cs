// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GbcNet.App.Database;

internal static class SqliteDbContextOptions
{
    internal static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string databaseFilePath
    ) => options.UseSqlite(CreateConnectionString(databaseFilePath));

    internal static string CreateConnectionString(string databaseFilePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            ForeignKeys = true,
        }.ToString();
}
