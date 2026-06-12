namespace GbcNet.Core.Ppu;

/// <summary>
/// CPU-external results produced by one PPU controller tick.
/// </summary>
internal readonly record struct PpuTickResult(LcdFrame? CompletedFrame, bool EnteredVisibleHBlank);
