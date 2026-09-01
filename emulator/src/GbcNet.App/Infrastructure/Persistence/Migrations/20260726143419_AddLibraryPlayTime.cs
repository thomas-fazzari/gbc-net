using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GbcNet.App.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryPlayTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "play_time_ticks",
                table: "roms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "play_time_ticks", table: "roms");
        }
    }
}
