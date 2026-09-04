// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Data.Common;
using System.Security.Cryptography;
using GbcNet.App.Entities;
using GbcNet.App.Infrastructure.Persistence;
using GbcNet.Core.Cheats;
using Microsoft.EntityFrameworkCore;

namespace GbcNet.App.Cheats;

internal readonly record struct CheatCodeEntry(CheatCode Code, bool IsEnabled, string? Name = null);

internal sealed class CheatCodeService(IDbContextFactory<GbcNetDbContext> dbContextFactory)
{
    internal const int MaxEntryCount = 20;
    internal const int MaxNameLength = 80;

    public async Task<CheatCodeEntry[]> LoadAsync(
        byte[] romHash,
        CancellationToken cancellationToken
    )
    {
        var storedRomHash = ValidateRomHash(romHash);

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var storedCodes = await db
                .CheatCodes.Where(entry => entry.RomHash == storedRomHash)
                .OrderBy(entry => entry.Type)
                .ThenBy(entry => entry.SortOrder)
                .Select(entry => new
                {
                    entry.Code,
                    entry.Name,
                    entry.IsEnabled,
                    entry.Type,
                })
                .ToArrayAsync(cancellationToken);
            var entries = new CheatCodeEntry[storedCodes.Length];

            for (var index = 0; index < storedCodes.Length; index++)
            {
                var storedCode = storedCodes[index];
                ValidateStoredName(storedCode.Name);

                if (
                    !CheatCode.TryParse(storedCode.Type, storedCode.Code, out var code)
                    || !string.Equals(storedCode.Code, code.CanonicalCode, StringComparison.Ordinal)
                )
                {
                    throw new FormatException("Stored cheat code is invalid.");
                }

                entries[index] = new CheatCodeEntry(code, storedCode.IsEnabled, storedCode.Name);
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
            throw new InvalidOperationException("Cheat codes could not be loaded.", exception);
        }
    }

    public async Task<CheatCodeEntry[]> ReplaceAsync(
        byte[] romHash,
        IReadOnlyList<CheatCodeEntry> entries,
        CancellationToken cancellationToken
    )
    {
        var storedRomHash = ValidateRomHash(romHash);
        ArgumentNullException.ThrowIfNull(entries);

        var normalizedEntries = new CheatCodeEntry[entries.Count];
        for (var index = 0; index < normalizedEntries.Length; index++)
        {
            normalizedEntries[index] = entries[index] with
            {
                Name = NormalizeName(entries[index].Name),
            };
        }

        ValidateEntries(normalizedEntries);

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken
            );

            await db
                .CheatCodes.Where(entry => entry.RomHash == storedRomHash)
                .ExecuteDeleteAsync(cancellationToken);

            var gameGenieSortOrder = 0;
            var gameSharkSortOrder = 0;
            foreach (var entry in normalizedEntries)
            {
                var sortOrder =
                    entry.Code.Type is CheatCodeType.GameGenie
                        ? gameGenieSortOrder++
                        : gameSharkSortOrder++;

                db.CheatCodes.Add(
                    StoredCheatCode.Create(
                        storedRomHash,
                        entry.Code.Type,
                        sortOrder,
                        entry.Code.CanonicalCode,
                        entry.IsEnabled,
                        entry.Name
                    )
                );
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return normalizedEntries;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            throw new InvalidOperationException("Cheat codes could not be saved.", exception);
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
                "Cheat code names must contain at most 80 characters.",
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
            throw new FormatException("Stored cheat code name is invalid.");
        }
    }

    private static void ValidateEntries(ReadOnlySpan<CheatCodeEntry> entries)
    {
        var gameGenieCount = 0;
        var gameSharkCount = 0;
        var codes =
            new HashSet<(CheatCodeType Type, ushort Address, byte Value, byte? CompareValue)>();

        foreach (var entry in entries)
        {
            if (
                entry.Code.Type is not (CheatCodeType.GameGenie or CheatCodeType.GameShark)
                || !CheatCode.TryParse(
                    entry.Code.Type,
                    entry.Code.CanonicalCode,
                    out var parsedCode
                )
                || parsedCode != entry.Code
            )
            {
                throw new ArgumentException(
                    "Cheat codes must be parsed successfully.",
                    nameof(entries)
                );
            }

            if (
                !codes.Add(
                    (entry.Code.Type, entry.Code.Address, entry.Code.Value, entry.Code.CompareValue)
                )
            )
            {
                throw new ArgumentException("Cheat codes must be unique by type.", nameof(entries));
            }

            if (
                (entry.Code.Type is CheatCodeType.GameGenie ? ++gameGenieCount : ++gameSharkCount)
                > MaxEntryCount
            )
            {
                throw new ArgumentException(
                    "A maximum of 20 cheat codes per type is allowed.",
                    nameof(entries)
                );
            }
        }
    }
}
