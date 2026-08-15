using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GbcNet.App.Database.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEntityMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = ON;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_cheat_codes_type",
                table: "cheat_codes");

            migrationBuilder.Sql(
                """UPDATE "cheat_codes" SET "type" = CASE "type" WHEN 0 THEN 'GameGenie' WHEN 1 THEN 'GameShark' END;"""
            );

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "cheat_codes",
                type: "TEXT",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "cheat_codes",
                type: "TEXT",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(11)");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "cheat_codes",
                type: "TEXT",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "rom_hash",
                table: "cheat_codes",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_cheat_codes_type",
                table: "cheat_codes",
                sql: "\"type\" IN ('GameGenie', 'GameShark')");

            migrationBuilder.Sql("PRAGMA ignore_check_constraints = OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA ignore_check_constraints = ON;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_cheat_codes_type",
                table: "cheat_codes");

            migrationBuilder.Sql(
                """UPDATE "cheat_codes" SET "type" = CASE "type" WHEN 'GameGenie' THEN 0 WHEN 'GameShark' THEN 1 END;"""
            );

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "cheat_codes",
                type: "varchar(80)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "cheat_codes",
                type: "varchar(11)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<int>(
                name: "type",
                table: "cheat_codes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 9);

            migrationBuilder.AlterColumn<string>(
                name: "rom_hash",
                table: "cheat_codes",
                type: "char(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.AddCheckConstraint(
                name: "CK_cheat_codes_type",
                table: "cheat_codes",
                sql: "\"type\" IN (0, 1)");

            migrationBuilder.Sql("PRAGMA ignore_check_constraints = OFF;");
        }
    }
}
