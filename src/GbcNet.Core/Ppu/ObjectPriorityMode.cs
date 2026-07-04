// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// Selects the CGB OBJ drawing-priority rule used after OAM scan selects a scanline's objects.
/// </summary>
internal enum ObjectPriorityMode : byte
{
    /// <summary>
    /// Earlier OAM entry wins, matching CGB OBJ priority.
    /// </summary>
    OamOrder = 0,

    /// <summary>
    /// Lower X coordinate wins, with OAM order breaking ties, matching DMG OBJ priority.
    /// </summary>
    LowerXWins = 1,
}
