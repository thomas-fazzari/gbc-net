// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text;

namespace GbcNet.App.Saves;

/// <summary>
/// Provides a stable ROM-derived filename stem and full content hash for persisted data.
/// </summary>
internal sealed class RomStorageIdentity
{
    private const string FallbackName = "GAME";

    private RomStorageIdentity(string fileStem, byte[] hash, string hashHex)
    {
        FileStem = fileStem;
        Hash = hash;
        HashHex = hashHex;
    }

    public string FileStem { get; }

    public byte[] Hash { get; }

    public string HashHex { get; }

    public static RomStorageIdentity Create(string title, ReadOnlySpan<byte> rom)
    {
        var hash = SHA256.HashData(rom);
        var hashHex = Convert.ToHexString(hash);
        return new($"{SanitizeName(title)}-{hashHex}", hash, hashHex);
    }

    private static string SanitizeName(string name)
    {
        StringBuilder builder = new(name.Length);

        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
            else if (character is ' ' or '-' or '_')
            {
                builder.Append('_');
            }
        }

        return builder.Length == 0 ? FallbackName : builder.ToString();
    }
}
