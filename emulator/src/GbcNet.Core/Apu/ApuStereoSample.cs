// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu;

/// <summary>
/// Signed PCM-friendly stereo APU sample after output conditioning.
/// </summary>
public readonly record struct ApuStereoSample(int Left, int Right);

/// <summary>
/// Internal analog stereo mix after channel DAC conversion, NR51 routing, and NR50 volume scaling.
/// </summary>
internal readonly record struct ApuAnalogStereoSample(double Left, double Right);
