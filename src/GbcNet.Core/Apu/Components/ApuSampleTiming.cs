// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Shared fixed-rate APU sample timing constants.
/// </summary>
internal static class ApuSampleTiming
{
    /// <summary>
    /// Default fixed output sample rate used by the core sample buffer.
    /// </summary>
    internal const int DefaultSampleRate = 48_000;

    /// <summary>
    /// Default bounded sample buffer capacity in stereo samples.
    /// </summary>
    internal const int DefaultSampleBufferCapacity = DefaultSampleRate / 10;
}
