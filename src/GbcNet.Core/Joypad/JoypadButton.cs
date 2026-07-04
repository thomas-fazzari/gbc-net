// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Joypad;

/// <summary>
/// Game Boy joypad buttons ordered by their P1/JOYP low-nibble bit mapping.
/// </summary>
public enum JoypadButton
{
    /// <summary>
    /// Direction Right, sharing JOYP bit 0 with A.
    /// </summary>
    Right = 0,

    /// <summary>
    /// Direction Left, sharing JOYP bit 1 with B.
    /// </summary>
    Left = 1,

    /// <summary>
    /// Direction Up, sharing JOYP bit 2 with Select.
    /// </summary>
    Up = 2,

    /// <summary>
    /// Direction Down, sharing JOYP bit 3 with Start.
    /// </summary>
    Down = 3,

    /// <summary>
    /// Action button A, sharing JOYP bit 0 with Right.
    /// </summary>
    A = 4,

    /// <summary>
    /// Action button B, sharing JOYP bit 1 with Left.
    /// </summary>
    B = 5,

    /// <summary>
    /// Select button, sharing JOYP bit 2 with Up.
    /// </summary>
    Select = 6,

    /// <summary>
    /// Start button, sharing JOYP bit 3 with Down.
    /// </summary>
    Start = 7,
}
