// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GbcNet.App.Database.Configurations;

internal sealed class StoredCheatCodeConfiguration : IEntityTypeConfiguration<StoredCheatCode>
{
    public void Configure(EntityTypeBuilder<StoredCheatCode> builder)
    {
        builder.ToTable(
            "cheat_codes",
            table =>
            {
                table.HasCheckConstraint("CK_cheat_codes_type", "\"type\" IN (0, 1)");
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
            .HasColumnType("char(64)");
        builder.Property(entry => entry.Type).HasColumnName("type");
        builder.Property(entry => entry.SortOrder).HasColumnName("sort_order");
        builder.Property(entry => entry.Code).HasColumnName("code").HasColumnType("varchar(11)");
        builder
            .Property(entry => entry.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(80)")
            .IsRequired(false);
        builder.Property(entry => entry.IsEnabled).HasColumnName("is_enabled");
    }
}
