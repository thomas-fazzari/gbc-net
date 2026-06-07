using System.Runtime.InteropServices;

namespace GbcNet.Core.Apu;

/// <summary>
/// Raw stereo mix from the APU before high-pass filter or backend conversion.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ApuStereoSample(int Left, int Right);
