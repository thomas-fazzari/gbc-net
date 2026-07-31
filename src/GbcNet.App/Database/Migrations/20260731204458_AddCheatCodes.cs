using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GbcNet.App.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCheatCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cheat_codes",
                columns: table => new
                {
                    rom_hash = table.Column<string>(type: "char(64)", nullable: false),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    code = table.Column<string>(type: "varchar(11)", nullable: false),
                    name = table.Column<string>(type: "varchar(80)", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheat_codes", x => new { x.rom_hash, x.type, x.sort_order });
                    table.CheckConstraint("CK_cheat_codes_is_enabled", "\"is_enabled\" IN (0, 1)");
                    table.CheckConstraint("CK_cheat_codes_name", "\"name\" IS NULL OR (length(\"name\") BETWEEN 1 AND 80 AND \"name\" = trim(\"name\"))");
                    table.CheckConstraint("CK_cheat_codes_sort_order", "\"sort_order\" BETWEEN 0 AND 19");
                    table.CheckConstraint("CK_cheat_codes_type", "\"type\" IN (0, 1)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cheat_codes_rom_hash_type_code",
                table: "cheat_codes",
                columns: new[] { "rom_hash", "type", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cheat_codes");
        }
    }
}
