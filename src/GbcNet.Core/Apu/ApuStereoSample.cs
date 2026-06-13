namespace GbcNet.Core.Apu;

/// <summary>
/// Signed PCM-friendly stereo APU sample after output conditioning.
/// </summary>
public readonly record struct ApuStereoSample(int Left, int Right);

/// <summary>
/// Internal raw digital mixer sample before DAC analog conversion and output filtering.
/// </summary>
internal readonly record struct ApuRawStereoSample(int Left, int Right);

/// <summary>
/// Internal analog mixer sample after channel DAC conversion and NR50 scaling.
/// </summary>
internal readonly record struct ApuAnalogStereoSample(double Left, double Right);
