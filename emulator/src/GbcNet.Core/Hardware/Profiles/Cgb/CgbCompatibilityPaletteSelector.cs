// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;

namespace GbcNet.Core.Hardware.Profiles;

internal readonly record struct CgbCompatibilityPalette(
    ushort Color0,
    ushort Color1,
    ushort Color2,
    ushort Color3
);

internal readonly record struct CgbCompatibilityPalettes(
    CgbCompatibilityPalette Background,
    CgbCompatibilityPalette ObjectPalette0,
    CgbCompatibilityPalette ObjectPalette1
);

internal readonly record struct CgbCompatibilityPaletteSelection(
    byte TitleChecksum,
    byte PaletteId,
    bool UsesCompatibilityLogoTilemap,
    CgbCompatibilityPalettes Palettes
);

/// <summary>
/// Selects the retail CGB DMG-compatibility palette set from cartridge header fields.
/// Data mirrored from the retail CGB boot ROM table as documented in Pan Docs.
/// </summary>
internal static class CgbCompatibilityPaletteSelector
{
    private const ushort TitleStartAddress = 0x0134;
    private const int TitleLength = 16;
    private const ushort FourthTitleByteAddress = TitleStartAddress + 3;
    private const ushort NewLicenseeCodeStartAddress = 0x0144;
    private const ushort OldLicenseeCodeAddress = 0x014B;
    private const byte OldNintendoLicenseeCode = 0x01;
    private const byte NewLicenseeMarker = 0x33;
    private const byte NewNintendoLicenseeCode0 = 0x30;
    private const byte NewNintendoLicenseeCode1 = 0x31;
    private const byte DefaultPaletteId = 0;
    private const int FirstDuplicateChecksumIndex = 65;

    /// For duplicate title checksums, the retail boot ROM breaks ties by checking the fourth title byte.
    /// The first character in this string matches checksum entry FirstDuplicateChecksumIndex,
    /// the second matches FirstDuplicateChecksumIndex + 1, and so on.
    private const string DuplicateFourthLetters = "BEFAARBEKEK R-URAR INAILICE R";

    // csharpier-ignore-start
    // Retail boot ROM title-checksum lookup table.
    // Indexes 0-64 map directly, 65+ need DuplicateFourthLetters too.
    private static readonly byte[] _titleChecksums =
    [
        0x00, 0x88, 0x16, 0x36, 0xD1, 0xDB, 0xF2, 0x3C, 0x8C, 0x92, 0x3D, 0x5C,
        0x58, 0xC9, 0x3E, 0x70, 0x1D, 0x59, 0x69, 0x19, 0x35, 0xA8, 0x14, 0xAA,
        0x75, 0x95, 0x99, 0x34, 0x6F, 0x15, 0xFF, 0x97, 0x4B, 0x90, 0x17, 0x10,
        0x39, 0xF7, 0xF6, 0xA2, 0x49, 0x4E, 0x43, 0x68, 0xE0, 0x8B, 0xF0, 0xCE,
        0x0C, 0x29, 0xE8, 0xB7, 0x86, 0x9A, 0x52, 0x01, 0x9D, 0x71, 0x9C, 0xBD,
        0x5D, 0x6D, 0x67, 0x3F, 0x6B, 0xB3, 0x46, 0x28, 0xA5, 0xC6, 0xD3, 0x27,
        0x61, 0x18, 0x66, 0x6A, 0xBF, 0x0D, 0xF4, 0xB3, 0x46, 0x28, 0xA5, 0xC6,
        0xD3, 0x27, 0x61, 0x18, 0x66, 0x6A, 0xBF, 0x0D, 0xF4, 0xB3,
    ];

    // Retail boot ROM palette ID table parallel to _titleChecksums.
    // Bit 7 means the DMG compatibility Nintendo logo tilemap is also written.
    private static readonly byte[] _paletteIdByChecksum =
    [
        0x00, 0x04, 0x05, 0x23, 0x22, 0x03, 0x1F, 0x0F, 0x0A, 0x05, 0x13, 0x24,
        0x87, 0x25, 0x1E, 0x2C, 0x15, 0x20, 0x1F, 0x14, 0x05, 0x21, 0x0D, 0x0E,
        0x05, 0x1D, 0x05, 0x12, 0x09, 0x03, 0x02, 0x1A, 0x19, 0x19, 0x29, 0x2A,
        0x1A, 0x2D, 0x2A, 0x2D, 0x24, 0x26, 0x9A, 0x2A, 0x1E, 0x29, 0x22, 0x22,
        0x05, 0x2A, 0x06, 0x05, 0x21, 0x19, 0x2A, 0x2A, 0x28, 0x02, 0x10, 0x19,
        0x2A, 0x2A, 0x05, 0x00, 0x27, 0x24, 0x16, 0x19, 0x06, 0x20, 0x0C, 0x24,
        0x0B, 0x27, 0x12, 0x27, 0x18, 0x1F, 0x32, 0x11, 0x2E, 0x06, 0x1B, 0x00,
        0x2F, 0x29, 0x29, 0x00, 0x00, 0x13, 0x22, 0x17, 0x12, 0x1D,
    ];

    // Each entry chooses OBJ0, OBJ1, BG palette starts inside _paletteColors.
    // Most values are 4-color palette indices multiplied by 4.
    // A few raw entries mirror the boot ROM exactly.
    private static readonly PaletteCombination[] _paletteCombinations =
    [
        new(16, 16, 116),
        new(72, 72, 72),
        new(80, 80, 80),
        new(96, 96, 96),
        new(36, 36, 36),
        new(0, 0, 0),
        new(108, 108, 108),
        new(20, 20, 20),
        new(48, 48, 48),
        new(104, 104, 104),
        new(64, 32, 32),
        new(16, 112, 112),
        new(16, 8, 8),
        new(12, 16, 16),
        new(16, 116, 116),
        new(112, 16, 112),
        new(8, 68, 8),
        new(64, 64, 32),
        new(16, 16, 28),
        new(16, 16, 72),
        new(16, 16, 80),
        new(76, 76, 36),
        new(15, 15, 44),
        new(68, 68, 8),
        new(16, 16, 8),
        new(16, 16, 12),
        new(112, 112, 0),
        new(12, 12, 0),
        new(0, 0, 4),
        new(72, 88, 72),
        new(80, 88, 80),
        new(96, 88, 96),
        new(64, 88, 32),
        new(68, 16, 52),
        new(111, 0, 56),
        new(111, 16, 60),
        new(76, 91, 36),
        new(64, 112, 40),
        new(16, 92, 112),
        new(68, 88, 8),
        new(16, 0, 8),
        new(16, 112, 12),
        new(112, 12, 0),
        new(12, 112, 16),
        new(84, 112, 16),
        new(12, 112, 0),
        new(100, 12, 112),
        new(0, 112, 32),
        new(16, 12, 112),
        new(112, 12, 24),
        new(16, 112, 116),
    ];

    // Raw RGB555 colors packed as boot-ROM palettes * 4 colors.
    private static readonly ushort[] _paletteColors =
    [
        0x7FFF, 0x32BF, 0x00D0, 0x0000, 0x639F, 0x4279, 0x15B0, 0x04CB,
        0x7FFF, 0x6E31, 0x454A, 0x0000, 0x7FFF, 0x1BEF, 0x0200, 0x0000,
        0x7FFF, 0x421F, 0x1CF2, 0x0000, 0x7FFF, 0x5294, 0x294A, 0x0000,
        0x7FFF, 0x03FF, 0x012F, 0x0000, 0x7FFF, 0x03EF, 0x01D6, 0x0000,
        0x7FFF, 0x42B5, 0x3DC8, 0x0000, 0x7E74, 0x03FF, 0x0180, 0x0000,
        0x67FF, 0x77AC, 0x1A13, 0x2D6B, 0x7ED6, 0x4BFF, 0x2175, 0x0000,
        0x53FF, 0x4A5F, 0x7E52, 0x0000, 0x4FFF, 0x7ED2, 0x3A4C, 0x1CE0,
        0x03ED, 0x7FFF, 0x255F, 0x0000, 0x036A, 0x021F, 0x03FF, 0x7FFF,
        0x7FFF, 0x01DF, 0x0112, 0x0000, 0x231F, 0x035F, 0x00F2, 0x0009,
        0x7FFF, 0x03EA, 0x011F, 0x0000, 0x299F, 0x001A, 0x000C, 0x0000,
        0x7FFF, 0x027F, 0x001F, 0x0000, 0x7FFF, 0x03E0, 0x0206, 0x0120,
        0x7FFF, 0x7EEB, 0x001F, 0x7C00, 0x7FFF, 0x3FFF, 0x7E00, 0x001F,
        0x7FFF, 0x03FF, 0x001F, 0x0000, 0x03FF, 0x001F, 0x000C, 0x0000,
        0x7FFF, 0x033F, 0x0193, 0x0000, 0x0000, 0x4200, 0x037F, 0x7FFF,
        0x7FFF, 0x7E8C, 0x7C00, 0x0000, 0x7FFF, 0x1BEF, 0x6180, 0x0000,
    ];
    // csharpier-ignore-end

    public static CgbCompatibilityPaletteSelection Default { get; } =
        CreateSelection(
            titleChecksum: 0,
            paletteId: DefaultPaletteId,
            usesCompatibilityLogoTilemap: false
        );

    public static CgbCompatibilityPaletteSelection Select(Cartridge cartridge)
    {
        if (!IsNintendoLicensee(cartridge))
        {
            return Default;
        }

        byte titleChecksum = 0;
        for (var offset = 0; offset < TitleLength; offset++)
        {
            titleChecksum = unchecked(
                (byte)(titleChecksum + cartridge.ReadRom((ushort)(TitleStartAddress + offset)))
            );
        }

        var checksumIndex = FindChecksumIndex(
            titleChecksum,
            cartridge.ReadRom(FourthTitleByteAddress)
        );
        if (checksumIndex < 0)
        {
            return Default with { TitleChecksum = titleChecksum };
        }

        var paletteIdWithFlags = _paletteIdByChecksum[checksumIndex];
        return CreateSelection(
            titleChecksum,
            (byte)(paletteIdWithFlags & 0x7F),
            usesCompatibilityLogoTilemap: (paletteIdWithFlags & 0x80) != 0
        );
    }

    private static int FindChecksumIndex(byte titleChecksum, byte fourthLetter)
    {
        for (var i = 0; i < _titleChecksums.Length; i++)
        {
            if (_titleChecksums[i] != titleChecksum)
            {
                continue;
            }

            if (
                i < FirstDuplicateChecksumIndex
                || DuplicateFourthLetters[i - FirstDuplicateChecksumIndex] == (char)fourthLetter
            )
            {
                return i;
            }
        }

        return -1;
    }

    private static CgbCompatibilityPaletteSelection CreateSelection(
        byte titleChecksum,
        byte paletteId,
        bool usesCompatibilityLogoTilemap
    )
    {
        var combination = _paletteCombinations[paletteId];

        return new CgbCompatibilityPaletteSelection(
            titleChecksum,
            paletteId,
            usesCompatibilityLogoTilemap,
            new CgbCompatibilityPalettes(
                ReadPalette(combination.BackgroundStartColor),
                ReadPalette(combination.Object0StartColor),
                ReadPalette(combination.Object1StartColor)
            )
        );
    }

    private static CgbCompatibilityPalette ReadPalette(byte startColor) =>
        new(
            _paletteColors[startColor],
            _paletteColors[startColor + 1],
            _paletteColors[startColor + 2],
            _paletteColors[startColor + 3]
        );

    private static bool IsNintendoLicensee(Cartridge cartridge)
    {
        var oldLicenseeCode = cartridge.ReadRom(OldLicenseeCodeAddress);

        return oldLicenseeCode == OldNintendoLicenseeCode
            || (
                oldLicenseeCode == NewLicenseeMarker
                && cartridge.ReadRom(NewLicenseeCodeStartAddress) == NewNintendoLicenseeCode0
                && cartridge.ReadRom(NewLicenseeCodeStartAddress + 1) == NewNintendoLicenseeCode1
            );
    }

    private readonly record struct PaletteCombination(
        byte Object0StartColor,
        byte Object1StartColor,
        byte BackgroundStartColor
    );
}
