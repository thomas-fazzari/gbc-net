// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Primary hardware family advertised by cartridge header flags.
/// </summary>
public enum CartridgeHardwareKind
{
    /// <summary>
    /// Original Game Boy cartridge with no CGB or SGB enhancement flag.
    /// </summary>
    GB = 0,

    /// <summary>
    /// Game Boy Color cartridge, either CGB-enhanced or CGB-required.
    /// </summary>
    GBC = 1,

    /// <summary>
    /// Super Game Boy-enhanced cartridge without CGB support.
    /// </summary>
    SGB = 2,
}
