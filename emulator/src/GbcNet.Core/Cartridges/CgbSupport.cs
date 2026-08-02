// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges;

/// <summary>
/// CGB compatibility declared by cartridge header byte 0143.
/// </summary>
public enum CgbSupport
{
    /// <summary>
    /// Cartridge does not request CGB features.
    /// </summary>
    None = 0,

    /// <summary>
    /// Cartridge supports CGB features but remains DMG compatible.
    /// </summary>
    Enhanced = 1,

    /// <summary>
    /// Cartridge requires CGB hardware.
    /// </summary>
    Required = 2,
}
