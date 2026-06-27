using Microsoft.Data.Sqlite;

namespace GbcNet.App.Library.Migrations;

internal static class V1CreateRoms
{
    public static readonly DbMigration Migration = new(1, Up);

    private static void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            create table roms (
                rom_hash text not null,
                last_known_path text not null,
                file_name text not null,
                cartridge_title text null,
                added_at text not null,
                last_opened_at text not null,
                launch_count integer not null default 0,
                cover_path text null
            );

            create unique index ix_roms_rom_hash on roms (rom_hash);
            create unique index ix_roms_last_known_path on roms (last_known_path);
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
    }
}
