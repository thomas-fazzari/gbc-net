// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.Core.Cartridges;
using Microsoft.EntityFrameworkCore;

namespace GbcNet.App.Library;

internal static class LibraryRomQueryableExtensions
{
    extension(IQueryable<LibraryRom> roms)
    {
        internal IQueryable<LibraryRomData> SelectData() =>
            roms.Select(rom => new LibraryRomData(
                rom.RomHash,
                rom.LastKnownPath,
                rom.FileName,
                rom.CartridgeTitle,
                rom.HardwareKind,
                rom.NoIntroHash,
                rom.AddedAt,
                rom.LastOpenedAt,
                rom.LaunchCount,
                rom.PlayTimeTicks,
                rom.CoverPath
            ));

        internal IOrderedQueryable<LibraryRom> ApplySort(LibraryQuery query)
        {
            var orderedRoms = query.Sort switch
            {
                LibrarySortField.LastOpened => query.IsAscending
                    ? roms.OrderBy(rom => rom.LastOpenedAt)
                    : roms.OrderByDescending(rom => rom.LastOpenedAt),
                LibrarySortField.MostPlayed => query.IsAscending
                    ? roms.OrderBy(rom => rom.LaunchCount)
                    : roms.OrderByDescending(rom => rom.LaunchCount),
                LibrarySortField.RecentlyAdded => query.IsAscending
                    ? roms.OrderBy(rom => rom.AddedAt)
                    : roms.OrderByDescending(rom => rom.AddedAt),
                LibrarySortField.MostTimePlayed => query.IsAscending
                    ? roms.OrderBy(rom => rom.PlayTimeTicks)
                    : roms.OrderByDescending(rom => rom.PlayTimeTicks),
                _ => throw new ArgumentOutOfRangeException(
                    paramName: nameof(query),
                    actualValue: query.Sort,
                    message: null
                ),
            };

            return orderedRoms.ThenBy(rom =>
                EF.Functions.Collate(
                    rom.FileName,
                    SqliteDbContextOptions.OrdinalIgnoreCaseCollation
                )
            );
        }
    }
}

internal sealed record LibraryRomData(
    string RomHash,
    string LastKnownPath,
    string FileName,
    string? CartridgeTitle,
    CartridgeHardwareKind HardwareKind,
    string? NoIntroHash,
    DateTimeOffset AddedAt,
    DateTimeOffset LastOpenedAt,
    int LaunchCount,
    long PlayTimeTicks,
    string? CoverPath
);
