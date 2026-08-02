// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Interrupts;

/// <summary>
/// Game Boy interrupt sources ordered by hardware priority.
/// </summary>
internal enum InterruptSource : byte
{
    /// <summary>
    /// VBlank interrupt, bit 0, vector 0040.
    /// </summary>
    VBlank = 0,

    /// <summary>
    /// LCD STAT interrupt, bit 1, vector 0048.
    /// </summary>
    LcdStat = 1,

    /// <summary>
    /// Timer interrupt, bit 2, vector 0050.
    /// </summary>
    Timer = 2,

    /// <summary>
    /// Serial interrupt, bit 3, vector 0058.
    /// </summary>
    Serial = 3,

    /// <summary>
    /// Joypad interrupt, bit 4, vector 0060.
    /// </summary>
    Joypad = 4,
}
