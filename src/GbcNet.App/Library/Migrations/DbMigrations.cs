using Microsoft.Data.Sqlite;

namespace GbcNet.App.Library.Migrations;

internal readonly record struct DbMigration(
    int Version,
    Action<SqliteConnection, SqliteTransaction> Apply
);

internal static class DbMigrations
{
    public static readonly DbMigration[] All = [V1CreateRoms.Migration];

    public const int LatestVersion = 1;
}
