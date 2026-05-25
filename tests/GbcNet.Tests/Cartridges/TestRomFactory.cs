using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.Cartridges;

internal static class TestRomFactory
{
    /// <summary>
    /// Official Nintendo logo bytes stored at cartridge header addresses 0104-0133.
    /// </summary>
    private static readonly byte[] NintendoLogo =
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
        byte[] rom = new byte[32 * 1024];
        NintendoLogo.CopyTo(rom, 0x0104);
        "TEST ROM"u8.CopyTo(rom.AsSpan(0x0134));
        rom[0x0147] = (byte)CartridgeType.RomOnly;
        rom[0x0148] = 0x00;
        rom[0x0149] = 0x00;

        configure?.Invoke(rom);

        rom[0x014D] = CartridgeHeader.CalculateHeaderChecksum(rom);
        return rom;
    }
}
