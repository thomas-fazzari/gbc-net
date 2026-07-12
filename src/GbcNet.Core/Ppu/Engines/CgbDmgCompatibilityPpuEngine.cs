// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

internal sealed record CgbDmgCompatibilityPpuEngineState(DmgPixelRulesPpuEngineState PixelRules)
    : IPpuEngineState;

/// <summary>
/// CGB DMG compatibility LCD engine using DMG pixel rules and CGB RGB555 palette output.
/// </summary>
internal sealed class CgbDmgCompatibilityPpuEngine
    : DmgPixelRulesPpuEngine<CgbDmgCompatibilityPixelOutput>
{
    protected override bool RequestsMode2InterruptBeforeVBlank => true;

    public override IPpuEngineState CaptureState() =>
        new CgbDmgCompatibilityPpuEngineState(CaptureDmgPixelRulesPpuEngineState());

    public override void ValidateState(IPpuEngineState state)
    {
        if (state is not CgbDmgCompatibilityPpuEngineState compatibilityState)
        {
            throw new ArgumentException(
                "PPU engine state must be for the CGB DMG compatibility engine.",
                nameof(state)
            );
        }

        ValidateDmgPixelRulesPpuEngineState(compatibilityState.PixelRules);
    }

    public override void RestoreState(IPpuEngineState state)
    {
        if (state is not CgbDmgCompatibilityPpuEngineState compatibilityState)
        {
            throw new ArgumentException(
                "PPU engine state must be for the CGB DMG compatibility engine.",
                nameof(state)
            );
        }

        ValidateDmgPixelRulesPpuEngineState(compatibilityState.PixelRules);
        RestoreDmgPixelRulesPpuEngineState(compatibilityState.PixelRules);
    }
}
