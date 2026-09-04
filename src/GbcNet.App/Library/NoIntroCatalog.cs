// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Frozen;
using System.Globalization;
using System.IO.Compression;

namespace GbcNet.App.Library;

/// <summary>
/// Regions assigned by No-Intro to a verified ROM dump.
/// </summary>
[Flags]
internal enum NoIntroRegion : uint
{
    None = 0,
    Argentina = 1 << 0,
    Asia = 1 << 1,
    Australia = 1 << 2,
    Brazil = 1 << 3,
    Canada = 1 << 4,
    China = 1 << 5,
    Denmark = 1 << 6,
    Europe = 1 << 7,
    Finland = 1 << 8,
    France = 1 << 9,
    Germany = 1 << 10,
    Greece = 1 << 11,
    HongKong = 1 << 12,
    Italy = 1 << 13,
    Japan = 1 << 14,
    Korea = 1 << 15,
    Netherlands = 1 << 16,
    Norway = 1 << 17,
    Russia = 1 << 18,
    Spain = 1 << 19,
    Sweden = 1 << 20,
    Taiwan = 1 << 21,
    Uk = 1 << 22,
    Usa = 1 << 23,
    World = 1 << 24,
}

/// <summary>
/// Canonical No-Intro title and verified release regions for one ROM dump.
/// </summary>
internal sealed record NoIntroMetadata(string Title, NoIntroRegion Regions);

/// <summary>
/// <para>
/// Read-only lookup of canonical Game Boy and Game Boy Color metadata from No-Intro.
/// </para>
/// <para>
/// The embedded index is derived from No-Intro's <c>Nintendo - Game Boy</c> and
/// <c>Nintendo - Game Boy Color</c> DAT sources, mirrored by
/// <a href="https://github.com/libretro/libretro-database">libretro-database</a>.
/// It contains hashes, titles, and regions.
/// </para>
/// <para>
/// It is loaded once into a <see cref="FrozenDictionary{TKey,TValue}"/> so library
/// lookups by persisted SHA-1 remain constant time.
/// </para>
/// </summary>
internal static class NoIntroCatalog
{
    private const string ResourceName = "GbcNet.App.Assets.Metadata.no-intro-index.gz";

    private static readonly FrozenDictionary<string, NoIntroMetadata> _entries = Load();

    /// <summary>
    /// Gets canonical metadata for a ROM SHA-1, or <see langword="null"/> when the dump
    /// is not in the embedded No-Intro snapshot.
    /// </summary>
    public static NoIntroMetadata? Get(string? sha1) =>
        sha1 is null ? null : _entries.GetValueOrDefault(sha1);

    /// <summary>
    /// Reads <c>SHA-1\tcanonical title\tregion bit mask</c> records from the generated index.
    /// </summary>
    private static FrozenDictionary<string, NoIntroMetadata> Load()
    {
        var assembly = typeof(NoIntroCatalog).Assembly;

        using var resource =
            assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {ResourceName}");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        var entries = new Dictionary<string, NoIntroMetadata>(
            capacity: 4_772,
            StringComparer.Ordinal
        );

        while (reader.ReadLine() is { } line)
        {
            var firstDelimiter = line.AsSpan().IndexOf('\t');
            var secondDelimiter = line.AsSpan()[(firstDelimiter + 1)..].IndexOf('\t');

            if (secondDelimiter >= 0)
            {
                secondDelimiter += firstDelimiter + 1;
            }

            if (
                firstDelimiter <= 0
                || secondDelimiter <= firstDelimiter + 1
                || !uint.TryParse(
                    line[(secondDelimiter + 1)..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var regions
                )
            )
            {
                throw new InvalidDataException("The embedded No-Intro index is malformed.");
            }

            entries.Add(
                line[..firstDelimiter],
                new NoIntroMetadata(
                    line[(firstDelimiter + 1)..secondDelimiter],
                    (NoIntroRegion)regions
                )
            );
        }

        return entries.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
