// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GbcNet.App.Infrastructure.Persistence.Configurations;

internal sealed class LibraryRomConfiguration : IEntityTypeConfiguration<LibraryRom>
{
    private const int FilePathMaxLength = 4096;

    public void Configure(EntityTypeBuilder<LibraryRom> builder)
    {
        builder.ToTable("roms");
        builder.HasKey(entry => entry.RomHash);
        builder.HasIndex(entry => entry.LastKnownPath).IsUnique();

        builder.Property(entry => entry.RomHash).HasColumnName("rom_hash").HasMaxLength(64);
        builder
            .Property(entry => entry.LastKnownPath)
            .HasColumnName("last_known_path")
            .HasMaxLength(FilePathMaxLength)
            .UseCollation(SqliteDbContextOptions.FileSystemPathCollation);
        builder.Property(entry => entry.FileName).HasColumnName("file_name").HasMaxLength(255);
        builder
            .Property(entry => entry.CartridgeTitle)
            .HasColumnName("cartridge_title")
            .HasMaxLength(16);
        builder
            .Property(entry => entry.HardwareKind)
            .HasColumnName("hardware_kind")
            .HasMaxLength(3);
        builder
            .Property(entry => entry.NoIntroHash)
            .HasColumnName("no_intro_hash")
            .HasMaxLength(40);
        builder.Property(entry => entry.AddedAt).HasColumnName("added_at");
        builder.Property(entry => entry.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entry => entry.LastOpenedAt).HasColumnName("last_opened_at");
        builder
            .Property(entry => entry.LaunchCount)
            .HasColumnName("launch_count")
            .HasDefaultValue(0);
        builder
            .Property(entry => entry.PlayTimeTicks)
            .HasColumnName("play_time_ticks")
            .HasDefaultValue(0);
        builder
            .Property(entry => entry.CoverPath)
            .HasColumnName("cover_path")
            .HasMaxLength(FilePathMaxLength);
    }
}
