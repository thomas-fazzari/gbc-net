// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;

namespace GbcNet.App.Audio;

/// <summary>
/// Receives conditioned emulator APU samples for GUI-side playback.
/// </summary>
internal interface IAudioOutput : IDisposable
{
    /// <summary>
    /// Queues stereo sample frames for playback.
    /// </summary>
    void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples);

    /// <summary>
    /// Applies the persistent user volume and mute settings.
    /// </summary>
    void SetVolume(int volumePercent, bool muted);

    /// <summary>
    /// Drops any queued samples that have not reached the audio device yet.
    /// </summary>
    void Clear();
}
