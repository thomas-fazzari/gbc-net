// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.Fixtures;

/// <summary>
/// Creates valid ROM-only cartridge images and loads cartridges for tests.
/// </summary>
internal static class TestRomFactory
{
    /// <summary>
    /// Official Nintendo logo bytes stored at cartridge header addresses 0104-0133.
    /// </summary>
    private static readonly byte[] _nintendoLogo =
    [
        0xCE,
        0xED,
        0x66,
        0x66,
        0xCC,
        0x0D,
        0x00,
        0x0B,
        0x03,
        0x73,
        0x00,
        0x83,
        0x00,
        0x0C,
        0x00,
        0x0D,
        0x00,
        0x08,
        0x11,
        0x1F,
        0x88,
        0x89,
        0x00,
        0x0E,
        0xDC,
        0xCC,
        0x6E,
        0xE6,
        0xDD,
        0xDD,
        0xD9,
        0x99,
        0xBB,
        0xBB,
        0x67,
        0x63,
        0x6E,
        0x0E,
        0xEC,
        0xCC,
        0xDD,
        0xDC,
        0x99,
        0x9F,
        0xBB,
        0xB9,
        0x33,
        0x3E,
    ];

    /// <summary>
    /// Creates a 32 KiB ROM-only image and optionally edits its bytes before checksumming it.
    /// </summary>
    /// <param name="configure">An optional callback invoked before the header checksum is written.</param>
    /// <returns>A valid test ROM image.</returns>
    public static byte[] Create(Action<byte[]>? configure = null)
    {
        return Create(romSizeCode: 0x00, configure);
    }

    /// <summary>
    /// Creates a ROM-only image with the requested cartridge-header size code.
    /// </summary>
    /// <param name="romSizeCode">A standard header value from 0x00 through 0x08.</param>
    /// <param name="configure">An optional callback invoked before the header checksum is written.</param>
    /// <returns>A valid test ROM image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="romSizeCode"/> is outside the supported range.
    /// </exception>
    public static byte[] Create(byte romSizeCode, Action<byte[]>? configure = null)
    {
        var rom = new byte[DecodeRomSizeBytes(romSizeCode)];
        _nintendoLogo.CopyTo(rom, 0x0104);
        "TEST ROM"u8.CopyTo(rom.AsSpan(0x0134));
        rom[0x0147] = (byte)CartridgeType.RomOnly;
        rom[0x0148] = romSizeCode;
        rom[0x0149] = 0x00;

        configure?.Invoke(rom);

        rom[0x014D] = CartridgeHeader.CalculateHeaderChecksum(rom);
        return rom;
    }

    /// <summary>
    /// Creates and loads a 32 KiB ROM-only cartridge.
    /// </summary>
    public static Cartridge LoadCartridge(Action<byte[]>? configure = null) =>
        LoadCartridge(Create(configure));

    /// <summary>
    /// Creates and loads a ROM-only cartridge with the requested header size code.
    /// </summary>
    public static Cartridge LoadCartridge(byte romSizeCode, Action<byte[]>? configure = null) =>
        LoadCartridge(Create(romSizeCode, configure));

    /// <summary>
    /// Loads a cartridge from an existing ROM image.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="rom"/> cannot be loaded as a supported cartridge.
    /// </exception>
    public static Cartridge LoadCartridge(byte[] rom) => Cartridge.LoadOrThrow(rom);

    /// <summary>
    /// Loads a cartridge with a deterministic Unix-time source for real-time clock tests.
    /// </summary>
    /// <param name="rom">The ROM image to load.</param>
    /// <param name="getUnixTimeSeconds">Returns seconds since the Unix epoch.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="rom"/> cannot be loaded as a supported cartridge.
    /// </exception>
    public static Cartridge LoadCartridge(byte[] rom, Func<long> getUnixTimeSeconds) =>
        Cartridge.LoadOrThrow(rom, getUnixTimeSeconds);

    private static int DecodeRomSizeBytes(byte code) =>
        code switch
        {
            <= 0x08 => 32 * 1024 * (1 << code),
            _ => throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "Test ROM size code must use the standard 32 KiB shifted range."
            ),
        };
}
