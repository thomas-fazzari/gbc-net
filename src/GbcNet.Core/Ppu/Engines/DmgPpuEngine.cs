namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// DMG LCD engine using DMG pixel rules and DMG shade-index frame output.
/// </summary>
internal sealed class DmgPpuEngine : DmgPixelRulesPpuEngine<DmgShadePixelOutput>;
