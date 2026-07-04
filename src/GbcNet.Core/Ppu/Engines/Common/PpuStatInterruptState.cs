// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Owns STAT interrupt line and LY=LYC comparison state for a PPU engine.
/// </summary>
internal sealed class PpuStatInterruptState
{
    private bool _statInterruptLine;
    private PpuMode _statInterruptMode;

    internal bool LycEqualsLy { get; private set; } = true;

    internal bool IsInterruptLineAsserted => _statInterruptLine;

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

    internal PpuInterruptRequest RefreshInterruptLine(
        byte statusInterruptSelect,
        bool lcdEnabled,
        bool requestInterrupt
    )
    {
        var statInterruptLine = CalculateInterruptLineAsserted(statusInterruptSelect, lcdEnabled);

        var requestLcdInterrupt = requestInterrupt && !_statInterruptLine && statInterruptLine;

        _statInterruptLine = statInterruptLine;

        return requestLcdInterrupt ? PpuInterruptRequest.LcdStat : PpuInterruptRequest.None;
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
        _statInterruptLine = false;
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
