using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GbcNet.App.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNoIntroCatalogHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "no_intro_hash",
                table: "roms",
                type: "TEXT",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "no_intro_hash",
                table: "roms");
        }
    }
}
