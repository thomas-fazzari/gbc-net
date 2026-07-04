// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// LCDC register bit masks.
/// </summary>
internal static class PpuLcdControlRegister
{
    /// <summary>
    /// LCDC bit 0 enables BG/window display on DMG and BG/window priority on CGB.
    /// </summary>
    public const byte BackgroundWindowEnableOrPriorityMask = 0x01;

    /// <summary>
    /// LCDC bit 1 enables OBJ display.
    /// </summary>
    public const byte ObjectEnableMask = 0x02;

    /// <summary>
    /// LCDC bit 2 selects 8x16 OBJ mode instead of 8x8.
    /// </summary>
    public const byte ObjectSizeMask = 0x04;

    /// <summary>
    /// LCDC bit 3 selects the BG tile map at 9C00-9FFF.
    /// </summary>
    public const byte BackgroundTileMapSelectMask = 0x08;

    /// <summary>
    /// LCDC bit 4 selects unsigned BG/window tile addressing at 8000-8FFF.
    /// </summary>
    public const byte BackgroundWindowTileDataSelectMask = 0x10;

    /// <summary>
    /// LCDC bit 5 enables Window display.
    /// </summary>
    public const byte WindowEnableMask = 0x20;

    /// <summary>
    /// LCDC bit 6 selects the Window tile map at 9C00-9FFF.
    /// </summary>
    public const byte WindowTileMapSelectMask = 0x40;

    /// <summary>
    /// LCDC bit 7 enables the LCD controller.
    /// </summary>
    public const byte LcdEnableMask = 0x80;
}
