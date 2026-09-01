// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Owns STAT interrupt line and LY=LYC comparison state for a PPU engine.
/// </summary>
internal sealed class PpuStatInterruptState
{
    private PpuMode _statInterruptMode;

    internal bool LycEqualsLy { get; private set; } = true;

    internal bool IsInterruptLineAsserted { get; private set; }

    internal PpuStatInterruptLatchState CaptureState() =>
        new(IsInterruptLineAsserted, _statInterruptMode, LycEqualsLy);

    internal static void ValidateState(PpuStatInterruptLatchState state)
    {
        if (
            state.InterruptMode
            is not (PpuMode.HBlank or PpuMode.VBlank or PpuMode.OamScan or PpuMode.Drawing)
        )
        {
            throw new ArgumentException("STAT interrupt mode is invalid.", nameof(state));
        }
    }

    internal void RestoreState(PpuStatInterruptLatchState state)
    {
        ValidateState(state);
        IsInterruptLineAsserted = state.IsInterruptLineAsserted;
        _statInterruptMode = state.InterruptMode;
        LycEqualsLy = state.LycEqualsLy;
    }

    internal void SetMode(PpuMode mode)
    {
        _statInterruptMode = mode;
    }

    internal void SetLycEqualsLyFromStatus(byte value)
    {
        LycEqualsLy = (value & PpuStatusRegister.LycEqualsLyMask) != 0;
    }

    internal void RefreshLycEqualsLy(PpuTiming timing, byte lcdYCompare)
    {
        LycEqualsLy =
            timing.IsLycCompareActiveOnCurrentDot() && timing.LcdYCoordinate == lcdYCompare;
    }

    internal PpuInterruptRequests RefreshInterruptLine(
        byte statusInterruptSelect,
        bool lcdEnabled,
        bool requestInterrupt
    )
    {
        var statInterruptLine = CalculateInterruptLineAsserted(statusInterruptSelect, lcdEnabled);

        var requestLcdInterrupt = requestInterrupt && !IsInterruptLineAsserted && statInterruptLine;

        IsInterruptLineAsserted = statInterruptLine;

        return requestLcdInterrupt ? PpuInterruptRequests.LcdStat : PpuInterruptRequests.None;
    }

    internal bool ShouldSuppressStableLycInterrupt(
        bool oldLycEqualsLy,
        byte statusInterruptSelect
    ) =>
        oldLycEqualsLy
        && LycEqualsLy
        && (statusInterruptSelect & PpuStatusRegister.LycEqualsLyInterruptSelectMask) != 0
        && (statusInterruptSelect & PpuStatusRegister.Mode0InterruptSelectMask) == 0;

    internal void ClearInterruptLine(PpuMode mode)
    {
        _statInterruptMode = mode;
        IsInterruptLineAsserted = false;
    }

    private bool CalculateInterruptLineAsserted(byte statusInterruptSelect, bool lcdEnabled)
    {
        if (!lcdEnabled)
        {
            return false;
        }

        return (
                statusInterruptSelect & PpuStatusRegister.GetInterruptSelectMask(_statInterruptMode)
            ) != 0
            || (
                LycEqualsLy
                && (statusInterruptSelect & PpuStatusRegister.LycEqualsLyInterruptSelectMask) != 0
            );
    }
}

internal readonly record struct PpuStatInterruptLatchState(
    bool IsInterruptLineAsserted,
    PpuMode InterruptMode,
    bool LycEqualsLy
);
