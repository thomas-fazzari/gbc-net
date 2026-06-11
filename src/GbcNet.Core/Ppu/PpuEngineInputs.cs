using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// CPU-visible PPU state consumed by the model-specific engine.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PpuEngineInputs(
    byte LcdControl,
    byte LcdYCompare,
    byte StatusInterruptSelect,
    byte ScrollY,
    byte ScrollX,
    byte WindowY,
    byte WindowX,
    byte BackgroundPalette,
    byte ObjectPalette0,
    byte ObjectPalette1,
    VideoRam VideoRam,
    MappedMemory ObjectAttributeMemory
);
