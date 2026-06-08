using System.Runtime.InteropServices;

namespace GbcNet.Gui.Emulation;

/// <summary>
/// Host-side emulation loop metrics used to diagnose pacing and throughput.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct EmulationMetrics(double TargetSpeed, double DisplayFramesPerSecond);
