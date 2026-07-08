// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using FluentResults;
using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.Core.Cartridges;
using Microsoft.EntityFrameworkCore;

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
    TimeProvider? timeProvider = null
)
{
    private readonly string _coverDirectoryPath = Path.GetFullPath(coverDirectoryPath);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<Result<string?>> RecordOpenedRomAsync(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var rom = await File.ReadAllBytesAsync(fullPath, CancellationToken.None)
                .ConfigureAwait(false);
            var cartridge = Cartridge.Load(rom);
            if (cartridge.IsFailed)
            {
                return Result.Fail(cartridge.Errors);
            }

            return RecordLoadedRom(fullPath, rom, cartridge.Value.Header);
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    public Result<string?> RecordLoadedRom(
        string path,
        ReadOnlyMemory<byte> rom,
        CartridgeHeader cartridgeHeader
    )
    {
        try
        {
            return Result.Ok(RecordOpenedRomCore(Path.GetFullPath(path), rom, cartridgeHeader));
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    public Result<IReadOnlyList<LibraryEntry>> GetRoms(int limit) => GetRoms(default, limit);

    public Result<IReadOnlyList<LibraryEntry>> GetRoms(
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

            return Result.Ok<IReadOnlyList<LibraryEntry>>([
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
            ]);
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    public Result RemoveRomPath(string path)
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
                DeleteManagedCoverFile(coverPath);
            }

            return Result.Ok();
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    public Result AssignCoverImage(string romHash, string sourceImagePath)
    {
        try
        {
            var previousCoverPath = GetCoverPath(romHash);
            if (previousCoverPath.IsFailed)
            {
                return Result.Fail(previousCoverPath.Errors);
            }

            var imageExtension = GetSafeImageExtension(sourceImagePath);
            if (imageExtension.IsFailed)
            {
                return Result.Fail(imageExtension.Errors);
            }

            Directory.CreateDirectory(_coverDirectoryPath);
            var destinationPath = Path.Combine(_coverDirectoryPath, romHash + imageExtension.Value);
            File.Copy(Path.GetFullPath(sourceImagePath), destinationPath, overwrite: true);
            SetCoverPath(romHash, destinationPath);
            DeleteManagedCoverFile(previousCoverPath.Value, destinationPath);

            return Result.Ok();
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    public Result ClearCover(string romHash)
    {
        try
        {
            var previousCoverPath = GetCoverPath(romHash);
            if (previousCoverPath.IsFailed)
            {
                return Result.Fail(previousCoverPath.Errors);
            }

            DeleteManagedCoverFile(previousCoverPath.Value);
            SetCoverPath(romHash, coverPath: null);

            return Result.Ok();
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
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
            .Roms.Where(rom =>
                rom.LastKnownPath == fullPath && rom.RomHash != romHash && rom.CoverPath != null
            )
            .Select(rom => rom.CoverPath!)
            .ToList();

        db.Roms.Where(rom => rom.LastKnownPath == fullPath && rom.RomHash != romHash)
            .ExecuteDelete();

        var existingRom = db.Roms.AsTracking().SingleOrDefault(rom => rom.RomHash == romHash);
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
            DeleteManagedCoverFile(coverPathToDelete);
        }

        return coverPath;
    }

    private Result<string?> GetCoverPath(string romHash)
    {
        using var db = dbContextFactory.CreateDbContext();
        var rom = db.Roms.SingleOrDefault(rom => rom.RomHash == romHash);
        return rom is null ? Result.Fail($"ROM not found: {romHash}") : Result.Ok(rom.CoverPath);
    }

    private void SetCoverPath(string romHash, string? coverPath)
    {
        using var db = dbContextFactory.CreateDbContext();
        var rom = db.Roms.AsTracking().Single(rom => rom.RomHash == romHash);
        rom.SetCoverPath(coverPath);
        db.SaveChanges();
    }

    private void DeleteManagedCoverFile(string? coverPath, string? exceptPath = null)
    {
        if (coverPath is null || !IsManagedCoverPath(coverPath))
        {
            return;
        }

        var fullCoverPath = Path.GetFullPath(coverPath);
        if (
            exceptPath is not null
            && string.Equals(
                fullCoverPath,
                Path.GetFullPath(exceptPath),
                GetFileSystemPathComparison()
            )
        )
        {
            return;
        }

        if (File.Exists(fullCoverPath))
        {
            File.Delete(fullCoverPath);
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

    private static Result<string> GetSafeImageExtension(string sourceImagePath)
    {
        var extension = Path.GetExtension(sourceImagePath);
        if (extension.Length is < 2 or > 16)
        {
            return Result.Fail("Cover image file name has no safe extension.");
        }

        for (var index = 1; index < extension.Length; index++)
        {
            if (!IsAsciiLetterOrDigit(extension[index]))
            {
                return Result.Fail("Cover image file name has no safe extension.");
            }
        }

        return Result.Ok(
            string.Create(
                extension.Length,
                extension,
                static (lowercaseExtension, extension) =>
                {
                    lowercaseExtension[0] = '.';
                    for (var index = 1; index < extension.Length; index++)
                    {
                        lowercaseExtension[index] = ToLowerAscii(extension[index]);
                    }
                }
            )
        );
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static char ToLowerAscii(char value) =>
        value is >= 'A' and <= 'Z' ? (char)(value + ('a' - 'A')) : value;

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
