// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.Cartridges;

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

    public static byte[] Create(Action<byte[]>? configure = null)
    {
        return Create(romSizeCode: 0x00, configure);
    }

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
