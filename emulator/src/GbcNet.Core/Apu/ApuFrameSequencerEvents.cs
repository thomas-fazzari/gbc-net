// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu;

/// <summary>
/// Frame sequencer events produced by one DIV-APU tick.
/// </summary>
internal readonly record struct ApuFrameSequencerEvents(
    bool LengthClock,
    bool SweepClock,
    bool EnvelopeClock
);
