namespace GbcNet.App.Emulation;

/// <summary>
/// Host-side emulation loop metrics used to diagnose pacing and throughput.
/// </summary>
internal readonly record struct EmulationMetrics(
    double SpeedMultiplier,
    double RenderedFramesPerSecond
);
