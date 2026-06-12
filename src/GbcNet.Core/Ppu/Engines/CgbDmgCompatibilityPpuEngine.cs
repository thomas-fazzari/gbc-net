namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB DMG compatibility LCD engine using DMG pixel rules and CGB RGB555 palette output.
/// </summary>
internal sealed class CgbDmgCompatibilityPpuEngine
    : DmgPixelRulesPpuEngine<CgbDmgCompatibilityPixelOutput>
{
    protected override bool RequestsMode2InterruptBeforeVBlank => true;
}
