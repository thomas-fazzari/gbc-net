// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.App.Sorting;
using GbcNet.Core.Cartridges;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Library;

internal readonly record struct LibraryQuery(
    string? SearchText = null,
    LibraryHardwareFilter Hardware = LibraryHardwareFilter.All,
    LibrarySortField Sort = LibrarySortField.LastOpened,
    LibraryRegionFilter Region = LibraryRegionFilter.All,
    SortDirection? SortDirection = null
);

internal enum LibraryHardwareFilter
{
    All = 0,
    Gb = 1,
    Gbc = 2,
    Sgb = 3,
}

internal enum LibraryRegionFilter
{
    All = 0,
    Japan = 1,
    Usa = 2,
    Europe = 3,
    World = 4,
    Other = 5,
}

internal enum LibrarySortField
{
    LastOpened = 0,
    Title = 1,
    MostPlayed = 2,
    RecentlyAdded = 3,
    MostTimePlayed = 4,
}

internal sealed class LibraryService(
    IDbContextFactory<GbcNetDbContext> dbContextFactory,
    string coverDirectoryPath,
    ILogger<LibraryService> logger,
    TimeProvider? timeProvider = null
)
{
    private readonly string _coverDirectoryPath = Path.GetFullPath(coverDirectoryPath);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<string?> RecordOpenedRomAsync(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var rom = await File.ReadAllBytesAsync(fullPath, CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);
            var cartridge = Cartridge.LoadOrThrow(rom);

            return RecordOpenedRomCore(fullPath, rom, cartridge.Header);
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
    }

    public string? RecordLoadedRom(
        string path,
        ReadOnlyMemory<byte> rom,
        CartridgeHeader cartridgeHeader
    )
    {
        try
        {
            return RecordOpenedRomCore(Path.GetFullPath(path), rom, cartridgeHeader);
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
    }

    public void RecordPlayTime(ReadOnlyMemory<byte> rom, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            var romHash = ComputeRomHash(rom.Span);
            using var db = dbContextFactory.CreateDbContext();
            var entry = db.Roms.AsTracking().SingleOrDefault(entry => entry.RomHash == romHash);

            if (entry is null)
            {
                return;
            }

            entry.AddPlayTime(duration);
            db.SaveChanges();
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
    }

    public IReadOnlyList<LibraryEntry> GetRoms(int limit) => GetRoms(query: default, limit);

    public IReadOnlyList<LibraryEntry> GetRoms(
        LibraryQuery query = default,
        int limit = int.MaxValue
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            using var db = dbContextFactory.CreateDbContext();
            IQueryable<LibraryRom> roms = db.Roms;

            var hardwareKind = GetHardwareKindFilter(query.Hardware);
            if (hardwareKind is not null)
            {
                roms = roms.Where(rom => rom.HardwareKind == hardwareKind);
            }

            var entries = roms.AsEnumerable()
                .Select(rom => new LibraryEntry(
                    rom.RomHash,
                    rom.LastKnownPath,
                    rom.FileName,
                    rom.CartridgeTitle,
                    rom.HardwareKind,
                    NoIntroCatalog.Get(rom.NoIntroHash),
                    rom.AddedAt,
                    rom.LastOpenedAt,
                    rom.LaunchCount,
                    TimeSpan.FromTicks(rom.PlayTimeTicks),
                    rom.CoverPath
                ));

            var searchText = NormalizeSearchText(query.SearchText);
            if (searchText is not null)
            {
                entries = entries.Where(entry =>
                    entry.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || entry.CartridgeTitle?.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase
                    )
                        is true
                    || entry.NoIntroMetadata?.Title.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase
                    )
                        is true
                );
            }

            entries = entries.Where(entry => MatchesRegion(entry.NoIntroMetadata, query.Region));

            var orderedEntries = OrderEntries(entries, query);

            return
            [
                .. orderedEntries
                    .ThenBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
                    .Take(limit),
            ];
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
    }

    private static IOrderedEnumerable<LibraryEntry> OrderEntries(
        IEnumerable<LibraryEntry> entries,
        LibraryQuery query
    )
    {
        var isAscending = query.SortDirection switch
        {
            SortDirection.Ascending => true,
            SortDirection.Descending => false,
            null => query.Sort == LibrarySortField.Title,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(query),
                actualValue: query.SortDirection,
                message: null
            ),
        };

        return query.Sort switch
        {
            LibrarySortField.LastOpened => isAscending
                ? entries.OrderBy(entry => entry.LastOpenedAt)
                : entries.OrderByDescending(entry => entry.LastOpenedAt),
            LibrarySortField.Title => isAscending
                ? entries.OrderBy(
                    entry => entry.NoIntroMetadata?.Title ?? entry.CartridgeTitle ?? entry.FileName,
                    StringComparer.OrdinalIgnoreCase
                )
                : entries.OrderByDescending(
                    entry => entry.NoIntroMetadata?.Title ?? entry.CartridgeTitle ?? entry.FileName,
                    StringComparer.OrdinalIgnoreCase
                ),
            LibrarySortField.MostPlayed => isAscending
                ? entries.OrderBy(entry => entry.LaunchCount)
                : entries.OrderByDescending(entry => entry.LaunchCount),
            LibrarySortField.RecentlyAdded => isAscending
                ? entries.OrderBy(entry => entry.AddedAt)
                : entries.OrderByDescending(entry => entry.AddedAt),
            LibrarySortField.MostTimePlayed => isAscending
                ? entries.OrderBy(entry => entry.PlayTime)
                : entries.OrderByDescending(entry => entry.PlayTime),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(query),
                actualValue: query.Sort,
                message: null
            ),
        };
    }

    public void RemoveRomPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            using var db = dbContextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();
            var coverPaths = db
                .Roms.Where(rom => rom.LastKnownPath == fullPath && rom.CoverPath != null)
                .Select(rom => rom.CoverPath!)
                .ToList();

            db.Roms.Where(rom => rom.LastKnownPath == fullPath).ExecuteDelete();
            transaction.Commit();

            foreach (var coverPath in coverPaths)
            {
                TryDeleteManagedCoverFileIfUnreferenced(coverPath);
            }
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
    }

    public void AssignCoverImage(string romHash, string sourceImagePath)
    {
        string? temporaryPath = null;
        string? destinationPath = null;
        var committed = false;

        try
        {
            using var db = dbContextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();
            var rom =
                db.Roms.AsTracking().SingleOrDefault(rom => rom.RomHash == romHash)
                ?? throw new InvalidOperationException("ROM not found: " + romHash);
            var previousCoverPath = rom.CoverPath;
            var imageExtension = GetSafeImageExtension(sourceImagePath);

            Directory.CreateDirectory(_coverDirectoryPath);
            var fileName = $"{romHash}-{Guid.NewGuid():N}{imageExtension}";
            temporaryPath = Path.Combine(path1: _coverDirectoryPath, path2: $".{fileName}.tmp");
            destinationPath = Path.Combine(path1: _coverDirectoryPath, path2: fileName);
            File.Copy(
                sourceFileName: Path.GetFullPath(sourceImagePath),
                destFileName: temporaryPath,
                overwrite: false
            );
            File.Move(sourceFileName: temporaryPath, destFileName: destinationPath);
            temporaryPath = null;

            rom.SetCoverPath(destinationPath);
            db.SaveChanges();
            transaction.Commit();
            committed = true;

            TryDeleteManagedCoverFileIfUnreferenced(previousCoverPath, destinationPath);
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            if (!committed)
            {
                TryDeleteManagedCoverFileIfUnreferenced(destinationPath);
                TryDeleteFile(temporaryPath);
            }

            throw CreateLibraryException(exception);
        }
    }

    public void ClearCover(string romHash)
    {
        try
        {
            using var db = dbContextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();
            var rom =
                db.Roms.AsTracking().SingleOrDefault(rom => rom.RomHash == romHash)
                ?? throw new InvalidOperationException("ROM not found: " + romHash);
            var previousCoverPath = rom.CoverPath;

            rom.SetCoverPath(coverPath: null);
            db.SaveChanges();
            transaction.Commit();

            TryDeleteManagedCoverFileIfUnreferenced(previousCoverPath);
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
    }

    private string? RecordOpenedRomCore(
        string fullPath,
        ReadOnlyMemory<byte> rom,
        CartridgeHeader cartridgeHeader
    )
    {
        var romHash = ComputeRomHash(rom.Span);
        var noIntroHash = ComputeNoIntroHash(rom.Span);
        var openedAt = _timeProvider.GetUtcNow();
        using var db = dbContextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();

        var deletedCoverPaths = db
            .Roms.Where(entry =>
                entry.LastKnownPath == fullPath
                && entry.RomHash != romHash
                && entry.CoverPath != null
            )
            .Select(entry => entry.CoverPath)
            .ToList();

        db.Roms.Where(entry => entry.LastKnownPath == fullPath && entry.RomHash != romHash)
            .ExecuteDelete();

        var existingRom = db.Roms.AsTracking().SingleOrDefault(entry => entry.RomHash == romHash);

        string? coverPath;
        if (existingRom is null)
        {
            coverPath = null;
            db.Roms.Add(
                LibraryRom.Opened(
                    romHash,
                    fullPath,
                    Path.GetFileName(fullPath),
                    cartridgeHeader.Title,
                    cartridgeHeader.HardwareKind,
                    noIntroHash,
                    openedAt
                )
            );
        }
        else
        {
            coverPath = existingRom.CoverPath;
            existingRom.RecordOpen(
                fullPath,
                Path.GetFileName(fullPath),
                cartridgeHeader.Title,
                cartridgeHeader.HardwareKind,
                noIntroHash,
                openedAt
            );
        }

        db.SaveChanges();
        transaction.Commit();

        foreach (var coverPathToDelete in deletedCoverPaths)
        {
            TryDeleteManagedCoverFileIfUnreferenced(coverPathToDelete);
        }

        return coverPath;
    }

    private void TryDeleteManagedCoverFileIfUnreferenced(
        string? coverPath,
        string? exceptPath = null
    )
    {
        if (coverPath is null)
        {
            return;
        }

        try
        {
            if (
                !IsManagedCoverPath(coverPath)
                || (
                    exceptPath is not null
                    && string.Equals(
                        Path.GetFullPath(coverPath),
                        Path.GetFullPath(exceptPath),
                        comparisonType: GetFileSystemPathComparison()
                    )
                )
            )
            {
                return;
            }

            using var db = dbContextFactory.CreateDbContext();
            if (!db.Roms.Any(rom => rom.CoverPath == coverPath))
            {
                TryDeleteFile(coverPath);
            }
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            LibraryServiceLog.CoverFileCleanupFailed(logger, exception);
        }
    }

    private void TryDeleteFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LibraryServiceLog.CoverFileCleanupFailed(logger, exception);
        }
    }

    private bool IsManagedCoverPath(string coverPath) =>
        Path.GetFullPath(coverPath)
            .StartsWith(
                EnsureTrailingDirectorySeparator(_coverDirectoryPath),
                GetFileSystemPathComparison()
            );

    private static string EnsureTrailingDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static StringComparison GetFileSystemPathComparison() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string GetSafeImageExtension(string sourceImagePath)
    {
        var extension = Path.GetExtension(sourceImagePath);
        if (extension.Length is < 2 or > 16)
        {
            throw new InvalidOperationException("Cover image file name has no safe extension.");
        }

        foreach (var value in extension.AsSpan(1))
        {
            if (!char.IsAsciiLetterOrDigit(value))
            {
                throw new InvalidOperationException("Cover image file name has no safe extension.");
            }
        }

        return string.Create(
            length: extension.Length,
            state: extension,
            action: static (result, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                {
                    var character = source[index];
                    result[index] = character is >= 'A' and <= 'Z'
                        ? (char)(character + ('a' - 'A'))
                        : character;
                }
            }
        );
    }

    private static string ComputeRomHash(ReadOnlySpan<byte> rom) =>
        Convert.ToHexString(SHA256.HashData(rom));

#pragma warning disable CA5350, S4790
    private static string ComputeNoIntroHash(ReadOnlySpan<byte> rom) =>
        Convert.ToHexString(SHA1.HashData(rom));
#pragma warning restore CA5350, S4790

    private static string? NormalizeSearchText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CartridgeHardwareKind? GetHardwareKindFilter(LibraryHardwareFilter hardware) =>
        hardware switch
        {
            LibraryHardwareFilter.All => null,
            LibraryHardwareFilter.Gb => CartridgeHardwareKind.GB,
            LibraryHardwareFilter.Gbc => CartridgeHardwareKind.GBC,
            LibraryHardwareFilter.Sgb => CartridgeHardwareKind.SGB,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(hardware),
                actualValue: hardware,
                message: null
            ),
        };

    private static bool MatchesRegion(NoIntroMetadata? metadata, LibraryRegionFilter region)
    {
        if (region is LibraryRegionFilter.All)
        {
            return true;
        }

        if (metadata is null)
        {
            return false;
        }

        var regions = metadata.Regions;
        return region switch
        {
            LibraryRegionFilter.Japan => regions.HasFlag(NoIntroRegion.Japan),
            LibraryRegionFilter.Usa => regions.HasFlag(NoIntroRegion.Usa),
            LibraryRegionFilter.Europe => regions.HasFlag(NoIntroRegion.Europe),
            LibraryRegionFilter.World => regions.HasFlag(NoIntroRegion.World),
            LibraryRegionFilter.Other => (
                regions
                & ~(
                    NoIntroRegion.Japan
                    | NoIntroRegion.Usa
                    | NoIntroRegion.Europe
                    | NoIntroRegion.World
                )
            ) != NoIntroRegion.None,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(region),
                actualValue: region,
                message: null
            ),
        };
    }

    private static InvalidOperationException CreateLibraryException(Exception exception) =>
        exception as InvalidOperationException
        ?? new InvalidOperationException(message: exception.Message, innerException: exception);

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or FormatException
                or NotSupportedException
                or ArgumentException
                or DbUpdateException;
}

internal static partial class LibraryServiceLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Cover file cleanup failed.")]
    internal static partial void CoverFileCleanupFailed(ILogger logger, Exception exception);
}
