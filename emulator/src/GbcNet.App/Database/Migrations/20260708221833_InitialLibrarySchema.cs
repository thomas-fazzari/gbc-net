using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GbcNet.App.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialLibrarySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roms",
                columns: table => new
                {
                    rom_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    last_known_path = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    file_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    cartridge_title = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    hardware_kind = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    added_at = table.Column<string>(type: "TEXT", maxLength: 33, nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", maxLength: 33, nullable: false),
                    last_opened_at = table.Column<string>(type: "TEXT", maxLength: 33, nullable: false),
                    launch_count = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    cover_path = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roms", x => x.rom_hash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roms_last_known_path",
                table: "roms",
                column: "last_known_path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roms");
        }
    }
}
