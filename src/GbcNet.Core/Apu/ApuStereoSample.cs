namespace GbcNet.Core.Apu;

/// <summary>
/// Signed PCM-friendly stereo APU sample after output conditioning.
/// </summary>
public readonly record struct ApuStereoSample(int Left, int Right);

/// <summary>
/// Internal digital stereo mix after NR50 volume and NR51 routing, before DAC analog conversion and output filtering.
/// </summary>
internal readonly record struct ApuMixedStereoSample(int Left, int Right);

/// <summary>
/// Internal analog stereo mix after channel DAC conversion, NR51 routing, and NR50 volume scaling.
/// </summary>
internal readonly record struct ApuAnalogStereoSample(double Left, double Right);
