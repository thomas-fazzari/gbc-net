using Microsoft.Data.Sqlite;

namespace GbcNet.App.Library.Migrations;

internal readonly record struct DbMigration(
    int Version,
    Action<SqliteConnection, SqliteTransaction> Apply
);

internal static class DbMigrations
{
    // Migrations are explicit instead of reflection-based for AOT compatibility
    private static readonly DbMigration[] _all = [V1CreateRoms.Migration];

    public static ReadOnlySpan<DbMigration> All => _all;

    public const int LatestVersion = 1;
}
