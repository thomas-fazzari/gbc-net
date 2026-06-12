namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Applies model-provided high-pass output filtering and signed PCM scaling to analog APU mix samples.
/// </summary>
internal sealed class ApuOutputFilter(double highPassChargeFactor)
{
    internal const double MaxAnalogMixerOutput = 4 * 8;
    internal const int PcmScale = short.MaxValue;

    private double _leftCapacitor;
    private double _rightCapacitor;

    /// <summary>
    /// Converts analog mixer output to signed PCM-friendly samples.
    /// </summary>
    public ApuStereoSample Filter(ApuAnalogStereoSample sample, bool anyDacEnabled)
    {
        if (!anyDacEnabled)
        {
            _leftCapacitor = 0;
            _rightCapacitor = 0;
            return default;
        }

        return new ApuStereoSample(
            Scale(HighPass(sample.Left, ref _leftCapacitor)),
            Scale(HighPass(sample.Right, ref _rightCapacitor))
        );
    }

    private double HighPass(double input, ref double capacitor)
    {
        var output = input - capacitor;
        capacitor = input - (output * highPassChargeFactor);
        return output;
    }

    private static int Scale(double sample) =>
        (int)
            Math.Clamp(
                Math.Round(sample / MaxAnalogMixerOutput * PcmScale, MidpointRounding.ToEven),
                short.MinValue,
                short.MaxValue
            );
}
