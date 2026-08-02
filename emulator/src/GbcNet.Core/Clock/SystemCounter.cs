// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Clock;

/// <summary>
/// Owns the 16-bit divider counter shared by DIV, TIMA, and serial clocks.
/// </summary>
internal sealed class SystemCounter
{
    private const int DividerVisibleShift = 8;

    /// <summary>
    /// Full 16-bit divider counter that feeds DIV, timer, and serial edge detection.
    /// </summary>
    public ushort DividerCounter { get; private set; }

    /// <summary>
    /// Reads the CPU-visible DIV register value from the high byte of the counter.
    /// </summary>
    public byte ReadDivider() => (byte)(DividerCounter >> DividerVisibleShift);

    /// <summary>
    /// Advances the counter by one machine cycle and returns bits that changed from high to low.
    /// </summary>
    public ushort AdvanceMachineCycle()
    {
        var previousValue = DividerCounter;
        DividerCounter = unchecked((ushort)(DividerCounter + HardwareTiming.MachineCycleTCycles));
        return GetFallingEdges(previousValue, DividerCounter);
    }

    /// <summary>
    /// Clears the counter as a DIV write would and returns bits that changed from high to low.
    /// </summary>
    public ushort Reset()
    {
        var previousValue = DividerCounter;
        DividerCounter = 0;
        return GetFallingEdges(previousValue, DividerCounter);
    }

    internal void SetDivider(byte value)
    {
        DividerCounter = (ushort)(value << DividerVisibleShift);
    }

    internal void SetCounter(ushort value)
    {
        DividerCounter = value;
    }

    private static ushort GetFallingEdges(ushort previousValue, ushort value) =>
        (ushort)(previousValue & ~value);
}
