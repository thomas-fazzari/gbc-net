using System.Runtime.InteropServices;

namespace GbcNet.Core.Ppu;

/// <summary>
/// CPU-visible PPU register snapshot used by model-specific timing logic.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PpuTimingInputs(
    byte LcdYCompare,
    byte StatusInterruptSelect,
    byte ScrollX
);
