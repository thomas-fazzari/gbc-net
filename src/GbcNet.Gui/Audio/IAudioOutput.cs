using GbcNet.Core.Apu;

namespace GbcNet.Gui.Audio;

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
    /// Drops any queued samples that have not reached the audio device yet.
    /// </summary>
    void Clear();
}
