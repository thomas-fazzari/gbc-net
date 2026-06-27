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

    public Result<IReadOnlyList<LibraryEntry>> GetRecentRoms(int limit)
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
                  cover_path
                from roms
                order by unixepoch(last_opened_at) desc
                limit $limit;
                """;
            command.Parameters.AddWithValue("$limit", limit);

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
                        reader.GetString(4),
                        reader.GetString(5),
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

    private void RecordOpenedRom(string fullPath, byte[] rom, Cartridge cartridge)
    {
        var romHash = ComputeRomHash(rom);
        var openedAt = _timeProvider.GetLocalNow().ToString("O", CultureInfo.InvariantCulture);
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText =
            "delete from roms where last_known_path = $lastKnownPath and rom_hash <> $romHash;";
        deleteCommand.Parameters.AddWithValue("$lastKnownPath", fullPath);
        deleteCommand.Parameters.AddWithValue("$romHash", romHash);
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
              cover_path
            ) values (
              $romHash,
              $lastKnownPath,
              $fileName,
              $cartridgeTitle,
              $openedAt,
              $openedAt,
              1,
              null
            )
            on conflict(rom_hash) do update set
              last_known_path = excluded.last_known_path,
              file_name = excluded.file_name,
              cartridge_title = excluded.cartridge_title,
              last_opened_at = excluded.last_opened_at,
              launch_count = roms.launch_count + 1;
            """;
        upsertCommand.Parameters.AddWithValue("$romHash", romHash);
        upsertCommand.Parameters.AddWithValue("$lastKnownPath", fullPath);
        upsertCommand.Parameters.AddWithValue("$fileName", Path.GetFileName(fullPath));
        upsertCommand.Parameters.AddWithValue("$cartridgeTitle", cartridge.Header.Title);
        upsertCommand.Parameters.AddWithValue("$openedAt", openedAt);
        upsertCommand.ExecuteNonQuery();

        transaction.Commit();
    }

    private static string ComputeRomHash(ReadOnlySpan<byte> rom) =>
        Convert.ToHexString(SHA256.HashData(rom));

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or SqliteException;
}
