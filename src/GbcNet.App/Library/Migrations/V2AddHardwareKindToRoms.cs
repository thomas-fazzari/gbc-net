using Microsoft.Data.Sqlite;

namespace GbcNet.App.Library.Migrations;

internal static class V2AddHardwareKindToRoms
{
    public static readonly DbMigration Migration = new(2, Up);

    private static void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var alterCommand = connection.CreateCommand();
        alterCommand.Transaction = transaction;
        alterCommand.CommandText =
            "alter table roms add column hardware_kind text not null default 'GB';";
        alterCommand.ExecuteNonQuery();

        using var versionCommand = connection.CreateCommand();
        versionCommand.Transaction = transaction;
        versionCommand.CommandText = "PRAGMA user_version = 2;";
        versionCommand.ExecuteNonQuery();
    }
}
