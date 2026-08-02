// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Applies model-provided high-pass output filtering and signed PCM scaling to analog APU mix samples.
/// </summary>
internal sealed class ApuOutputFilter(double highPassChargeFactor)
{
    internal const double MaxAnalogMixerOutput = 4 * 8;

    private const int PcmScale = short.MaxValue;
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

    internal ApuOutputFilterState CaptureState() => new(_leftCapacitor, _rightCapacitor);

    internal static void ValidateState(ApuOutputFilterState state)
    {
        if (
            state
            is not {
                LeftCapacitor: >= -MaxAnalogMixerOutput and <= MaxAnalogMixerOutput,
                RightCapacitor: >= -MaxAnalogMixerOutput and <= MaxAnalogMixerOutput,
            }
        )
        {
            throw new ArgumentException(
                "Capacitors must be finite and within the analog mixer output range.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(ApuOutputFilterState state)
    {
        ValidateState(state);
        _leftCapacitor = state.LeftCapacitor;
        _rightCapacitor = state.RightCapacitor;
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

internal readonly record struct ApuOutputFilterState(double LeftCapacitor, double RightCapacitor);
