// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu;

/// <summary>
/// Runtime hardware signals consumed by one APU machine-cycle tick.
/// </summary>
internal readonly record struct ApuTickInputs(
    ushort SystemCounterFallingEdges,
    bool CgbDoubleSpeed
);
