using System.Runtime.InteropServices;

namespace GbcNet.Core.Apu;

/// <summary>
/// Stereo mix from the APU core before High Pass Filter/backend conversion.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ApuStereoSample(int Left, int Right);
