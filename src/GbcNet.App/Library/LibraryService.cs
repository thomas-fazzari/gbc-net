// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Security.Cryptography;
using FluentResults;
using GbcNet.App.Library.Entities;
using GbcNet.Core.Cartridges;
using Microsoft.Data.Sqlite;

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
    LibraryDatabase database,
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
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                select
                  rom_hash,
                  last_known_path,
                  file_name,
                  cartridge_title,
                  added_at,
                  last_opened_at,
                  launch_count,
                  cover_path,
                  hardware_kind
                from roms
                where (
                  $searchText is null
                  or upper(file_name) like $searchText escape '\'
                  or upper(coalesce(cartridge_title, '')) like $searchText escape '\'
                )
                and ($hardwareKind is null or hardware_kind = $hardwareKind)
                and (
                  $coverFilter = 0
                  or ($coverFilter = 1 and cover_path is not null)
                  or ($coverFilter = 2 and cover_path is null)
                )
                order by
                  case when $sortMode = 0 then last_opened_at end desc,
                  case when $sortMode = 1 then coalesce(cartridge_title, file_name) end collate nocase asc,
                  case when $sortMode = 2 then launch_count end desc,
                  case when $sortMode = 3 then added_at end desc,
                  file_name collate nocase asc
                limit $limit;
                """;
            AddOptionalTextParameter(command, "$searchText", NormalizeSearchText(query.SearchText));
            AddOptionalTextParameter(
                command,
                "$hardwareKind",
                GetHardwareKindFilter(query.Hardware)
            );
            AddIntegerParameter(command, "$coverFilter", (int)query.Cover);
            AddIntegerParameter(command, "$sortMode", (int)query.Sort);
            AddIntegerParameter(command, "$limit", limit);

            using var reader = command.ExecuteReader();
            var entries = new List<LibraryEntry>();
            while (reader.Read())
            {
                entries.Add(ReadLibraryEntry(reader));
            }

            return Result.Ok<IReadOnlyList<LibraryEntry>>(entries);
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
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var coverPaths = new List<string>();

            using var selectCoverCommand = connection.CreateCommand();
            selectCoverCommand.Transaction = transaction;
            selectCoverCommand.CommandText =
                "select cover_path from roms where last_known_path = $lastKnownPath and cover_path is not null;";
            AddTextParameter(selectCoverCommand, "$lastKnownPath", fullPath);
            using (var reader = selectCoverCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    coverPaths.Add(reader.GetString(0));
                }
            }

            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "delete from roms where last_known_path = $lastKnownPath;";
            AddTextParameter(deleteCommand, "$lastKnownPath", fullPath);
            deleteCommand.ExecuteNonQuery();

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
        var openedAt = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var deletedCoverPaths = new List<string>();

        using var selectDeletedCoverCommand = connection.CreateCommand();
        selectDeletedCoverCommand.Transaction = transaction;
        selectDeletedCoverCommand.CommandText =
            "select cover_path from roms where last_known_path = $lastKnownPath and rom_hash <> $romHash and cover_path is not null;";
        AddTextParameter(selectDeletedCoverCommand, "$lastKnownPath", fullPath);
        AddTextParameter(selectDeletedCoverCommand, "$romHash", romHash);
        using (var reader = selectDeletedCoverCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                deletedCoverPaths.Add(reader.GetString(0));
            }
        }

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText =
            "delete from roms where last_known_path = $lastKnownPath and rom_hash <> $romHash;";
        AddTextParameter(deleteCommand, "$lastKnownPath", fullPath);
        AddTextParameter(deleteCommand, "$romHash", romHash);
        deleteCommand.ExecuteNonQuery();

        using var upsertCommand = connection.CreateCommand();
        upsertCommand.Transaction = transaction;
        upsertCommand.CommandText = """
            insert into roms (
              rom_hash,
              last_known_path,
              file_name,
              cartridge_title,
              added_at,
              last_opened_at,
              launch_count,
              cover_path,
              hardware_kind
            ) values (
              $romHash,
              $lastKnownPath,
              $fileName,
              $cartridgeTitle,
              $openedAt,
              $openedAt,
              1,
              null,
              $hardwareKind
            )
            on conflict(rom_hash) do update set
              last_known_path = excluded.last_known_path,
              file_name = excluded.file_name,
              cartridge_title = excluded.cartridge_title,
              hardware_kind = excluded.hardware_kind,
              last_opened_at = excluded.last_opened_at,
              launch_count = roms.launch_count + 1
            returning cover_path;
            """;
        AddTextParameter(upsertCommand, "$romHash", romHash);
        AddTextParameter(upsertCommand, "$lastKnownPath", fullPath);
        AddTextParameter(upsertCommand, "$fileName", Path.GetFileName(fullPath));
        AddOptionalTextParameter(upsertCommand, "$cartridgeTitle", cartridgeHeader.Title);
        AddTextParameter(upsertCommand, "$hardwareKind", cartridgeHeader.HardwareKind.ToString());
        AddTextParameter(upsertCommand, "$openedAt", openedAt);
        var coverPath = upsertCommand.ExecuteScalar() as string;

        transaction.Commit();
        foreach (var coverPathToDelete in deletedCoverPaths)
        {
            DeleteManagedCoverFile(coverPathToDelete);
        }

        return coverPath;
    }

    private Result<string?> GetCoverPath(string romHash)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "select cover_path from roms where rom_hash = $romHash;";
        AddTextParameter(command, "$romHash", romHash);

        var value = command.ExecuteScalar();
        if (value is null)
        {
            return Result.Fail($"ROM not found: {romHash}");
        }

        return Result.Ok(value == DBNull.Value ? null : (string)value);
    }

    private void SetCoverPath(string romHash, string? coverPath)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "update roms set cover_path = $coverPath where rom_hash = $romHash;";
        AddOptionalTextParameter(command, "$coverPath", coverPath);
        AddTextParameter(command, "$romHash", romHash);
        command.ExecuteNonQuery();
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
        string.IsNullOrWhiteSpace(value)
            ? null
            : $"%{EscapeLike(value.Trim().ToUpperInvariant())}%";

    private static string EscapeLike(string value) =>
        value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private static string? GetHardwareKindFilter(LibraryHardwareFilter hardware) =>
        hardware switch
        {
            LibraryHardwareFilter.All => null,
            LibraryHardwareFilter.Gb => nameof(CartridgeHardwareKind.GB),
            LibraryHardwareFilter.Gbc => nameof(CartridgeHardwareKind.GBC),
            LibraryHardwareFilter.Sgb => nameof(CartridgeHardwareKind.SGB),
            _ => throw new ArgumentOutOfRangeException(nameof(hardware), hardware, message: null),
        };

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static CartridgeHardwareKind ParseHardwareKind(string value) =>
        value switch
        {
            nameof(CartridgeHardwareKind.GB) => CartridgeHardwareKind.GB,
            nameof(CartridgeHardwareKind.GBC) => CartridgeHardwareKind.GBC,
            nameof(CartridgeHardwareKind.SGB) => CartridgeHardwareKind.SGB,
            _ => throw new FormatException($"Unknown cartridge hardware kind '{value}'."),
        };

    private static LibraryEntry ReadLibraryEntry(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            ParseHardwareKind(reader.GetString(8)),
            ParseTimestamp(reader.GetString(4)),
            ParseTimestamp(reader.GetString(5)),
            reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7)
        );

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or FormatException
                or NotSupportedException
                or ArgumentException
                or SqliteException;

    private static void AddTextParameter(SqliteCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.SqliteType = SqliteType.Text;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddOptionalTextParameter(SqliteCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.SqliteType = SqliteType.Text;
        parameter.Value = value ?? (object)DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddIntegerParameter(SqliteCommand command, string name, int value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.SqliteType = SqliteType.Integer;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
