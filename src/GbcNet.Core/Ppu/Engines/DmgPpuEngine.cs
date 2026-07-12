// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

internal sealed record DmgPpuEngineState(DmgPixelRulesPpuEngineState PixelRules) : IPpuEngineState;

/// <summary>
/// DMG LCD engine using DMG pixel rules and DMG shade-index frame output.
/// </summary>
internal sealed class DmgPpuEngine : DmgPixelRulesPpuEngine<DmgShadePixelOutput>
{
    public override IPpuEngineState CaptureState() =>
        new DmgPpuEngineState(CaptureDmgPixelRulesPpuEngineState());

    public override void ValidateState(IPpuEngineState state)
    {
        if (state is not DmgPpuEngineState dmgState)
        {
            throw new ArgumentException(
                "PPU engine state must be for the DMG engine.",
                nameof(state)
            );
        }

        ValidateDmgPixelRulesPpuEngineState(dmgState.PixelRules);
    }

    public override void RestoreState(IPpuEngineState state)
    {
        if (state is not DmgPpuEngineState dmgState)
        {
            throw new ArgumentException(
                "PPU engine state must be for the DMG engine.",
                nameof(state)
            );
        }

        ValidateDmgPixelRulesPpuEngineState(dmgState.PixelRules);
        RestoreDmgPixelRulesPpuEngineState(dmgState.PixelRules);
    }
}
