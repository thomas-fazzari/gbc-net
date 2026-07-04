// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// Interrupts requested by a PPU timing transition.
/// </summary>
[Flags]
internal enum PpuInterruptRequest : byte
{
    /// <summary>
    /// No interrupt requested.
    /// </summary>
    None = 0,

    /// <summary>
    /// Request the VBlank interrupt.
    /// </summary>
    VBlank = 1 << 0,

    /// <summary>
    /// Request the LCD STAT interrupt.
    /// </summary>
    LcdStat = 1 << 1,
}
