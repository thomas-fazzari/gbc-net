// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration.Sections.Audio;

internal sealed record AudioConfig(int VolumePercent = 100, bool Muted = false)
{
    public const int MinimumVolumePercent = 0;
    public const int MaximumVolumePercent = 100;

    public static bool IsValidVolume(int volumePercent) =>
        volumePercent is >= MinimumVolumePercent and <= MaximumVolumePercent;
}
