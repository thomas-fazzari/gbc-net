// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cheats;

/// <summary>
/// Supported cheat code families.
/// </summary>
public enum CheatCodeType
{
    /// <summary>
    /// Game Genie (https://gamegenie.com/cheats/gamegenie/gameboy/index.html)
    /// </summary>
    /// <remarks>
    /// ROM read replacement codes.
    /// </remarks>
    GameGenie = 0,

    /// <summary>
    /// GameShark (https://gamegenie.com/cheats/gameshark/gbcolor/)
    /// </summary>
    /// <remarks>
    /// Periodic RAM write codes.
    /// </remarks>
    GameShark = 1,
}
