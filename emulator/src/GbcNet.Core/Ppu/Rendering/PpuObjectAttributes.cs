// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// OAM object attribute layout and scanline limits.
/// </summary>
internal static class PpuObjectAttributes
{
    /// <summary>
    /// OAM contains 40 OBJ entries.
    /// </summary>
    public const int ObjectCount = 40;

    /// <summary>
    /// OAM scan can select at most 10 OBJ entries for a scanline.
    /// </summary>
    public const int MaxObjectsPerScanline = 10;

    /// <summary>
    /// Each OBJ entry contains Y, X, tile, and flags.
    /// </summary>
    public const int AttributeSize = 4;

    /// <summary>
    /// Y coordinate byte offset inside one OBJ entry.
    /// </summary>
    public const int YCoordinateOffset = 0;

    /// <summary>
    /// X coordinate byte offset inside one OBJ entry.
    /// </summary>
    public const int XCoordinateOffset = 1;

    /// <summary>
    /// Tile index byte offset inside one OBJ entry.
    /// </summary>
    public const int TileIndexOffset = 2;

    /// <summary>
    /// Flags byte offset inside one OBJ entry.
    /// </summary>
    public const int FlagsOffset = 3;

    /// <summary>
    /// OAM stores OBJ Y as screen Y plus 16.
    /// </summary>
    public const int YScreenOffset = 16;

    /// <summary>
    /// OAM stores OBJ X as screen X plus eight.
    /// </summary>
    public const int XScreenOffset = 8;

    /// <summary>
    /// OBJ height when LCDC bit 2 is clear.
    /// </summary>
    public const int Size8 = 8;

    /// <summary>
    /// OBJ height when LCDC bit 2 is set.
    /// </summary>
    public const int Size16 = 16;

    /// <summary>
    /// OBJ with X>=168 is fully hidden on the right side.
    /// </summary>
    public const byte FirstFullyHiddenRightX = 168;

    /// <summary>
    /// OBJ flag bits 0-2 select one of eight CGB object palettes.
    /// </summary>
    public const byte CgbPaletteMask = 0x07;

    /// <summary>
    /// OBJ flag bit 3 selects VRAM bank 1 instead of bank 0 on CGB.
    /// </summary>
    public const byte CgbTileBankMask = 0x08;

    /// <summary>
    /// OBJ flag bit 4 selects OBP1 instead of OBP0 on DMG.
    /// </summary>
    public const byte DmgPalette1Mask = 0x10;

    /// <summary>
    /// OBJ flag bit 5 mirrors tile pixels horizontally.
    /// </summary>
    public const byte XFlipMask = 0x20;

    /// <summary>
    /// OBJ flag bit 6 mirrors tile pixels vertically.
    /// </summary>
    public const byte YFlipMask = 0x40;

    /// <summary>
    /// OBJ flag bit 7 lets non-zero BG/window pixels cover this OBJ.
    /// </summary>
    public const byte BackgroundPriorityMask = 0x80;
}
