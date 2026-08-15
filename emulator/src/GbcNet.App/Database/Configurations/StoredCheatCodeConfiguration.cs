// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GbcNet.App.Database.Configurations;

internal sealed class StoredCheatCodeConfiguration : IEntityTypeConfiguration<StoredCheatCode>
{
    private const int RomHashMaxLength = 64;
    private const int CheatCodeTypeMaxLength = 9;
    private const int CheatCodeMaxLength = 11;
    private const int NameMaxLength = 80;

    public void Configure(EntityTypeBuilder<StoredCheatCode> builder)
    {
        builder.ToTable(
            "cheat_codes",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_cheat_codes_type",
                    "\"type\" IN ('GameGenie', 'GameShark')"
                );
                table.HasCheckConstraint(
                    "CK_cheat_codes_sort_order",
                    "\"sort_order\" BETWEEN 0 AND 19"
                );
                table.HasCheckConstraint("CK_cheat_codes_is_enabled", "\"is_enabled\" IN (0, 1)");
                table.HasCheckConstraint(
                    "CK_cheat_codes_name",
                    "\"name\" IS NULL OR (length(\"name\") BETWEEN 1 AND 80 AND \"name\" = trim(\"name\"))"
                );
            }
        );
        builder.HasKey(entry => new
        {
            entry.RomHash,
            entry.Type,
            entry.SortOrder,
        });
        builder
            .HasIndex(entry => new
            {
                entry.RomHash,
                entry.Type,
                entry.Code,
            })
            .IsUnique();

        builder
            .Property(entry => entry.RomHash)
            .HasColumnName("rom_hash")
            .HasMaxLength(RomHashMaxLength);
        builder
            .Property(entry => entry.Type)
            .HasColumnName("type")
            .HasMaxLength(CheatCodeTypeMaxLength);
        builder.Property(entry => entry.SortOrder).HasColumnName("sort_order");
        builder
            .Property(entry => entry.Code)
            .HasColumnName("code")
            .HasMaxLength(CheatCodeMaxLength);
        builder.Property(entry => entry.Name).HasColumnName("name").HasMaxLength(NameMaxLength);
        builder.Property(entry => entry.IsEnabled).HasColumnName("is_enabled");
    }
}
