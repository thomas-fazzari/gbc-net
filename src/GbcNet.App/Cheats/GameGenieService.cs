// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Data.Common;
using System.Security.Cryptography;
using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.Core.Cheats;
using Microsoft.EntityFrameworkCore;

namespace GbcNet.App.Cheats;

internal readonly record struct GameGenieCodeEntry(
    GameGenieCode Code,
    bool IsEnabled,
    string? Name = null
);

internal sealed class GameGenieService(IDbContextFactory<GbcNetDbContext> dbContextFactory)
{
    internal const int MaxEntryCount = 20;
    internal const int MaxNameLength = 80;

    public async Task<GameGenieCodeEntry[]> LoadAsync(
        byte[] romHash,
        CancellationToken cancellationToken
    )
    {
        var storedRomHash = ValidateRomHash(romHash);

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var storedCodes = await db
                .CheatCodes.Where(entry =>
                    entry.RomHash == storedRomHash && entry.Type == CheatCodeType.GameGenie
                )
                .OrderBy(entry => entry.SortOrder)
                .Select(entry => new
                {
                    entry.Code,
                    entry.Name,
                    entry.IsEnabled,
                })
                .ToArrayAsync(cancellationToken);
            var entries = new GameGenieCodeEntry[storedCodes.Length];

            for (var index = 0; index < storedCodes.Length; index++)
            {
                var storedCode = storedCodes[index];
                if (
                    !GameGenieCode.TryParse(storedCode.Code, out var code)
                    || !string.Equals(storedCode.Code, code.CanonicalCode, StringComparison.Ordinal)
                )
                {
                    throw new FormatException("Stored Game Genie code is invalid.");
                }

                ValidateStoredName(storedCode.Name);
                entries[index] = new GameGenieCodeEntry(
                    code,
                    storedCode.IsEnabled,
                    storedCode.Name
                );
            }

            ValidateEntries(entries);
            return entries;
        }
        catch (Exception exception)
            when (exception
                    is DbException
                        or DbUpdateException
                        or FormatException
                        or ArgumentException
            )
        {
            throw new InvalidOperationException("Game Genie codes could not be loaded.", exception);
        }
    }

    public async Task<GameGenieCodeEntry[]> ReplaceAsync(
        byte[] romHash,
        IReadOnlyList<GameGenieCodeEntry> entries,
        CancellationToken cancellationToken
    )
    {
        var storedRomHash = ValidateRomHash(romHash);
        ArgumentNullException.ThrowIfNull(entries);

        var validatedEntries = entries.ToArray();
        for (var index = 0; index < validatedEntries.Length; index++)
        {
            validatedEntries[index] = validatedEntries[index] with
            {
                Name = NormalizeName(validatedEntries[index].Name),
            };
        }

        ValidateEntries(validatedEntries);

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken
            );

            await db
                .CheatCodes.Where(entry =>
                    entry.RomHash == storedRomHash && entry.Type == CheatCodeType.GameGenie
                )
                .ExecuteDeleteAsync(cancellationToken);

            for (var index = 0; index < validatedEntries.Length; index++)
            {
                var entry = validatedEntries[index];
                db.CheatCodes.Add(
                    new StoredCheatCode(
                        storedRomHash,
                        CheatCodeType.GameGenie,
                        index,
                        entry.Code.CanonicalCode,
                        entry.IsEnabled,
                        entry.Name
                    )
                );
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return validatedEntries;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            throw new InvalidOperationException("Game Genie codes could not be saved.", exception);
        }
    }

    private static string ValidateRomHash(byte[] romHash)
    {
        ArgumentNullException.ThrowIfNull(romHash);

        if (romHash.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException("ROM hash must contain 32 bytes.", nameof(romHash));
        }

        return Convert.ToHexString(romHash);
    }

    private static string? NormalizeName(string? name)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            return null;
        }

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                "Game Genie code names must contain at most 80 characters.",
                nameof(name)
            );
        }

        return normalizedName;
    }

    private static void ValidateStoredName(string? name)
    {
        if (
            name is not null
            && (
                name.Length is 0 or > MaxNameLength
                || !string.Equals(name, name.Trim(), StringComparison.Ordinal)
            )
        )
        {
            throw new FormatException("Stored Game Genie code name is invalid.");
        }
    }

    private static void ValidateEntries(ReadOnlySpan<GameGenieCodeEntry> entries)
    {
        if (entries.Length > MaxEntryCount)
        {
            throw new ArgumentException(
                "A maximum of 20 Game Genie codes is allowed.",
                nameof(entries)
            );
        }

        var effectiveCodes =
            new HashSet<(ushort Address, byte ReplacementValue, byte? CompareValue)>();
        foreach (var entry in entries)
        {
            if (entry.Code.CanonicalCode.Length == 0)
            {
                throw new ArgumentException(
                    "Game Genie codes must be parsed successfully.",
                    nameof(entries)
                );
            }

            if (
                !effectiveCodes.Add(
                    (entry.Code.Address, entry.Code.ReplacementValue, entry.Code.CompareValue)
                )
            )
            {
                throw new ArgumentException("Game Genie codes must be unique.", nameof(entries));
            }
        }
    }
}
