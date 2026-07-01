using System.Globalization;
using System.Security.Cryptography;
using FluentResults;
using GbcNet.App.Library.Entities;
using GbcNet.Core.Cartridges;
using Microsoft.Data.Sqlite;

namespace GbcNet.App.Library;

internal sealed class LibraryService(LibraryDatabase database, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<Result> RecordOpenedRomAsync(string path)
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

            RecordOpenedRom(fullPath, rom, cartridge.Value);
            return Result.Ok();
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    public Result<IReadOnlyList<LibraryEntry>> GetRoms(int limit = int.MaxValue)
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
                order by last_opened_at desc
                limit $limit;
                """;
            AddIntegerParameter(command, "$limit", limit);

            using var reader = command.ExecuteReader();
            var entries = new List<LibraryEntry>();
            while (reader.Read())
            {
                entries.Add(
                    new LibraryEntry(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        ParseHardwareKind(reader.GetString(8)),
                        ParseTimestamp(reader.GetString(4)),
                        ParseTimestamp(reader.GetString(5)),
                        reader.GetInt32(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7)
                    )
                );
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
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "delete from roms where last_known_path = $lastKnownPath;";

            AddTextParameter(command, "$lastKnownPath", Path.GetFullPath(path));

            command.ExecuteNonQuery();
            return Result.Ok();
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            return Result.Fail(exception.Message);
        }
    }

    private void RecordOpenedRom(string fullPath, byte[] rom, Cartridge cartridge)
    {
        var romHash = ComputeRomHash(rom);
        var openedAt = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

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
              launch_count = roms.launch_count + 1;
            """;
        AddTextParameter(upsertCommand, "$romHash", romHash);
        AddTextParameter(upsertCommand, "$lastKnownPath", fullPath);
        AddTextParameter(upsertCommand, "$fileName", Path.GetFileName(fullPath));
        AddOptionalTextParameter(upsertCommand, "$cartridgeTitle", cartridge.Header.Title);
        AddTextParameter(upsertCommand, "$hardwareKind", cartridge.Header.HardwareKind.ToString());
        AddTextParameter(upsertCommand, "$openedAt", openedAt);
        upsertCommand.ExecuteNonQuery();

        transaction.Commit();
    }

    private static string ComputeRomHash(ReadOnlySpan<byte> rom) =>
        Convert.ToHexString(SHA256.HashData(rom));

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

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or FormatException
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
