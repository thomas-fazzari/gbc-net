// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.Core.Cartridges;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Library;

internal readonly record struct LibraryQuery(
    string? SearchText = null,
    LibraryHardwareFilter Hardware = LibraryHardwareFilter.All,
    LibraryCoverFilter Cover = LibraryCoverFilter.All,
    LibrarySortMode Sort = LibrarySortMode.LastOpened
);

internal enum LibraryHardwareFilter
{
    All = 0,
    Gb = 1,
    Gbc = 2,
    Sgb = 3,
}

internal enum LibraryCoverFilter
{
    All = 0,
    WithCover = 1,
    MissingCover = 2,
}

internal enum LibrarySortMode
{
    LastOpened = 0,
    Title = 1,
    MostPlayed = 2,
    RecentlyAdded = 3,
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
                .ConfigureAwait(false);
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

    public IReadOnlyList<LibraryEntry> GetRoms(int limit) => GetRoms(default, limit);

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

            var searchText = NormalizeSearchText(query.SearchText);
            if (searchText is not null)
            {
                roms = roms.Where(rom =>
                    EF.Functions.Like(
                        EF.Functions.Collate(
                            rom.FileName,
                            GbcNetDbConstants.CaseInsensitiveCollation
                        ),
                        searchText,
                        @"\"
                    )
                    || EF.Functions.Like(
                        EF.Functions.Collate(
                            rom.CartridgeTitle ?? string.Empty,
                            GbcNetDbConstants.CaseInsensitiveCollation
                        ),
                        searchText,
                        @"\"
                    )
                );
            }

            var hardwareKind = GetHardwareKindFilter(query.Hardware);
            if (hardwareKind is not null)
            {
                roms = roms.Where(rom => rom.HardwareKind == hardwareKind);
            }

            roms = query.Cover switch
            {
                LibraryCoverFilter.All => roms,
                LibraryCoverFilter.WithCover => roms.Where(rom => rom.CoverPath != null),
                LibraryCoverFilter.MissingCover => roms.Where(rom => rom.CoverPath == null),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(query),
                    query.Cover,
                    message: null
                ),
            };

            var orderedRoms = query.Sort switch
            {
                LibrarySortMode.LastOpened => roms.OrderByDescending(rom => rom.LastOpenedAt),
                LibrarySortMode.Title => roms.OrderBy(rom =>
                    EF.Functions.Collate(
                        rom.CartridgeTitle ?? rom.FileName,
                        GbcNetDbConstants.CaseInsensitiveCollation
                    )
                ),
                LibrarySortMode.MostPlayed => roms.OrderByDescending(rom => rom.LaunchCount),
                LibrarySortMode.RecentlyAdded => roms.OrderByDescending(rom => rom.AddedAt),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(query),
                    query.Sort,
                    message: null
                ),
            };

            return
            [
                .. orderedRoms
                    .ThenBy(rom =>
                        EF.Functions.Collate(
                            rom.FileName,
                            GbcNetDbConstants.CaseInsensitiveCollation
                        )
                    )
                    .Take(limit)
                    .Select(rom => new LibraryEntry(
                        rom.RomHash,
                        rom.LastKnownPath,
                        rom.FileName,
                        rom.CartridgeTitle,
                        rom.HardwareKind,
                        rom.AddedAt,
                        rom.LastOpenedAt,
                        rom.LaunchCount,
                        rom.CoverPath
                    )),
            ];
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            throw CreateLibraryException(exception);
        }
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
            temporaryPath = Path.Combine(_coverDirectoryPath, $".{fileName}.tmp");
            destinationPath = Path.Combine(_coverDirectoryPath, fileName);
            File.Copy(Path.GetFullPath(sourceImagePath), temporaryPath, overwrite: false);
            File.Move(temporaryPath, destinationPath);
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

            rom.SetCoverPath(null);
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
                        GetFileSystemPathComparison()
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
            LibraryServiceLog.CoverFileCleanupFailed(logger, coverPath, exception);
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
            LibraryServiceLog.CoverFileCleanupFailed(logger, path, exception);
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
            extension.Length,
            extension,
            static (result, source) =>
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

    private static string? NormalizeSearchText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"%{EscapeLike(value.Trim())}%";

    private static string EscapeLike(string value) =>
        value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private static CartridgeHardwareKind? GetHardwareKindFilter(LibraryHardwareFilter hardware) =>
        hardware switch
        {
            LibraryHardwareFilter.All => null,
            LibraryHardwareFilter.Gb => CartridgeHardwareKind.GB,
            LibraryHardwareFilter.Gbc => CartridgeHardwareKind.GBC,
            LibraryHardwareFilter.Sgb => CartridgeHardwareKind.SGB,
            _ => throw new ArgumentOutOfRangeException(nameof(hardware), hardware, message: null),
        };

    private static InvalidOperationException CreateLibraryException(Exception exception) =>
        exception as InvalidOperationException
        ?? new InvalidOperationException(exception.Message, exception);

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
    [LoggerMessage(Level = LogLevel.Warning, Message = "Cover file cleanup failed for {Path}.")]
    internal static partial void CoverFileCleanupFailed(
        ILogger logger,
        string path,
        Exception exception
    );
}
