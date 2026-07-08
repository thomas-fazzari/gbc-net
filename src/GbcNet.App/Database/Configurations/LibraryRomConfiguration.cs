// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.App.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GbcNet.App.Database.Configurations;

internal sealed class LibraryRomConfiguration : IEntityTypeConfiguration<LibraryRom>
{
    private const int FilePathMaxLength = 4096;
    private const int TimestampMaxLength = 33;

    private static readonly ValueConverter<DateTimeOffset, string> _timestampConverter = new(
        timestamp => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        value =>
            DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    );

    public void Configure(EntityTypeBuilder<LibraryRom> builder)
    {
        builder.ToTable("roms");
        builder.HasKey(entry => entry.RomHash);
        builder.HasIndex(entry => entry.LastKnownPath).IsUnique();

        builder.Property(entry => entry.RomHash).HasColumnName("rom_hash").HasMaxLength(64);
        builder
            .Property(entry => entry.LastKnownPath)
            .HasColumnName("last_known_path")
            .HasMaxLength(FilePathMaxLength);
        builder.Property(entry => entry.FileName).HasColumnName("file_name").HasMaxLength(255);
        builder
            .Property(entry => entry.CartridgeTitle)
            .HasColumnName("cartridge_title")
            .HasMaxLength(16);
        builder
            .Property(entry => entry.HardwareKind)
            .HasColumnName("hardware_kind")
            .HasConversion<string>()
            .HasMaxLength(3);
        builder
            .Property(entry => entry.AddedAt)
            .HasColumnName("added_at")
            .HasConversion(_timestampConverter)
            .HasMaxLength(TimestampMaxLength);
        builder
            .Property(entry => entry.UpdatedAt)
            .HasColumnName("updated_at")
            .HasConversion(_timestampConverter)
            .HasMaxLength(TimestampMaxLength);
        builder
            .Property(entry => entry.LastOpenedAt)
            .HasColumnName("last_opened_at")
            .HasConversion(_timestampConverter)
            .HasMaxLength(TimestampMaxLength);
        builder
            .Property(entry => entry.LaunchCount)
            .HasColumnName("launch_count")
            .HasDefaultValue(0);
        builder
            .Property(entry => entry.CoverPath)
            .HasColumnName("cover_path")
            .HasMaxLength(FilePathMaxLength);
    }
}
