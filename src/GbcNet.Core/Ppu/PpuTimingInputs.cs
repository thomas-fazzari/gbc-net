using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// CPU-visible PPU register snapshot used by model-specific timing logic.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PpuTimingInputs(
    byte LcdControl,
    byte LcdYCompare,
    byte StatusInterruptSelect,
    byte ScrollX,
    MappedMemory ObjectAttributeMemory
);
