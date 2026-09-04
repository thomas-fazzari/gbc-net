// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GbcNet.App.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePlatformPathIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "last_known_path",
                table: "roms",
                type: "TEXT",
                maxLength: 4096,
                nullable: false,
                collation: "GBCNET_FILE_SYSTEM_PATH",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 4096
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "last_known_path",
                table: "roms",
                type: "TEXT",
                maxLength: 4096,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 4096,
                oldCollation: "GBCNET_FILE_SYSTEM_PATH"
            );
        }
    }
}
