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
    /// Default bounded sample buffer capacity, currently 100 ms at the default sample rate.
    /// </summary>
    internal const int DefaultCapacity = DefaultSampleRate / 10;
}
