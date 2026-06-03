namespace GbcNet.Core.Ppu;

/// <summary>
/// Hardware-visible results produced by one PPU engine tick.
/// </summary>
internal readonly record struct PpuEngineTickResult(
    PpuInterruptRequest Interrupts,
    LcdFrame? CompletedFrame
);
