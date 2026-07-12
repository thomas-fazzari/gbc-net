// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Timers;

namespace GbcNet.Tests.Timers;

public sealed class TimerControllerTests
{
    private const byte TimerInterrupt = 0b0000_0100;

    [Fact]
    public void TickMachineCycle_AdvancesDividerVisibleByteEvery64MachineCycles()
    {
        var (counter, timers) = CreateTimers();

        TickMachineCycles(counter, timers, 63);
        Assert.Equal(0x00, counter.ReadDivider());

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x01, counter.ReadDivider());
    }

    [Fact]
    public void ResetDivider_ClearsSystemCounter()
    {
        var (counter, timers) = CreateTimers();
        TickMachineCycles(counter, timers, 128);

        ResetSystemCounter(counter, timers);

        Assert.Equal(0x00, counter.ReadDivider());
    }

    [Fact]
    public void TickMachineCycle_DoesNotIncrementTimerCounterWhenTimerIsDisabled()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0001);

        TickMachineCycles(counter, timers, 256);

        Assert.Equal(0x00, timers.TimerCounter);
    }

    [Fact]
    public void TickMachineCycle_AdvancesDividerWhenTimerIsDisabled()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0001);

        TickMachineCycles(counter, timers, 128);

        Assert.Equal(0x02, counter.ReadDivider());
    }

    [Theory]
    [InlineData(0b0000_0100, 256)]
    [InlineData(0b0000_0101, 4)]
    [InlineData(0b0000_0110, 16)]
    [InlineData(0b0000_0111, 64)]
    public void TickMachineCycle_IncrementsTimerCounterAtSelectedCadence(
        byte timerControl,
        int machineCycles
    )
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(timerControl);

        TickMachineCycles(counter, timers, machineCycles - 1);
        Assert.Equal(0x00, timers.TimerCounter);

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_UsesUpdatedClockSelectForSubsequentTicks()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 4);
        Assert.Equal(0x01, timers.TimerCounter);
        ResetSystemCounter(counter, timers);

        timers.WriteTimerControl(0b0000_0110);
        TickMachineCycles(counter, timers, 15);
        Assert.Equal(0x01, timers.TimerCounter);

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x02, timers.TimerCounter);
    }

    [Fact]
    public void ResetDivider_IncrementsTimerCounterWhenSelectedBitFalls()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        ResetSystemCounter(counter, timers);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenSelectedBitFalls()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        timers.WriteTimerControl(0b0000_0110);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenDisablingHighSelectedBitIfProfileTicksOnDisable()
    {
        var (counter, timers) = CreateTimers(ticksOnTacDisableWhenInputHigh: true);
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        timers.WriteTimerControl(0b0000_0001);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_DoesNotIncrementTimerCounterWhenDisablingHighSelectedBitIfProfileDoesNotTickOnDisable()
    {
        var (counter, timers) = CreateTimers(ticksOnTacDisableWhenInputHigh: false);
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        timers.WriteTimerControl(0b0000_0001);

        Assert.Equal(0x00, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenEnablingHighSelectedBitIfProfileTicksOnEnable()
    {
        var (counter, timers) = CreateTimers(ticksOnTacEnableWhenInputHigh: true);
        TickMachineCycles(counter, timers, 128);

        timers.WriteTimerControl(0b0000_0100);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_DoesNotIncrementTimerCounterWhenEnablingHighSelectedBitIfProfileDoesNotTickOnEnable()
    {
        var (counter, timers) = CreateTimers(ticksOnTacEnableWhenInputHigh: false);
        TickMachineCycles(counter, timers, 128);

        timers.WriteTimerControl(0b0000_0100);

        Assert.Equal(0x00, timers.TimerCounter);
    }

    [Fact]
    public void TickMachineCycle_ReloadsTimerModuloAndRequestsTimerInterruptOneMachineCycleAfterOverflow()
    {
        var interrupts = new InterruptController();
        var (counter, timers) = CreateTimers(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);

        TickMachineCycles(counter, timers, 4);

        Assert.Equal(0x00, timers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x42, timers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteTimerCounter_CancelsPendingOverflowReload()
    {
        var interrupts = new InterruptController();
        var (counter, timers) = CreateTimers(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 4);

        timers.WriteTimerCounter(0x99);
        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x99, timers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteTimerCounter_DuringReloadMachineCycleIsIgnored()
    {
        var (counter, timers) = CreateTimers();
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 5);

        timers.WriteTimerCounter(0x99);

        Assert.Equal(0x42, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerModulo_DuringPendingReloadUpdatesReloadedCounter()
    {
        var interrupts = new InterruptController();
        var (counter, timers) = CreateTimers(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 4);

        timers.WriteTimerModulo(0x77);
        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x77, timers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadTimerControl_ReturnsUnusedBitsSet()
    {
        var (_, timers) = CreateTimers();

        timers.WriteTimerControl(0b0000_0101);

        Assert.Equal(0b1111_1101, timers.ReadTimerControl());
    }

    [Fact]
    public void CaptureRestoreState_PreservesRawRegistersForNextDividerFallingEdge()
    {
        var (sourceCounter, sourceTimers) = CreateTimers();
        sourceCounter.SetCounter(0x00FC);
        sourceTimers.TimerCounter = 0x3A;
        sourceTimers.TimerModulo = 0x6D;
        sourceTimers.SetTimerControlState(0b0000_0101);

        var (restoredCounter, restoredTimers) = CreateTimers();
        restoredCounter.SetCounter(0x00FC);
        restoredTimers.RestoreState(sourceTimers.CaptureState());
        restoredTimers.TickSystemCounter(restoredCounter.AdvanceMachineCycle());

        Assert.Equal(0x3B, restoredTimers.TimerCounter);
        Assert.Equal(0x6D, restoredTimers.TimerModulo);
    }

    [Fact]
    public void CaptureRestoreState_PreservesPendingOverflowWithoutRequestingInterrupt()
    {
        var (sourceCounter, sourceTimers) = CreateTimers();
        sourceTimers.TimerCounter = 0xFF;
        sourceTimers.TimerModulo = 0x42;
        sourceTimers.SetTimerControlState(0b0000_0101);
        TickMachineCycles(sourceCounter, sourceTimers, 4);

        var interrupts = new InterruptController();
        var (_, restoredTimers) = CreateTimers(interrupts);
        restoredTimers.RestoreState(sourceTimers.CaptureState());

        Assert.Equal(0x00, restoredTimers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);

        restoredTimers.AdvanceOverflowReload();

        Assert.Equal(0x42, restoredTimers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void CaptureRestoreState_PreservesReloadWriteBlockedPhase()
    {
        var (sourceCounter, sourceTimers) = CreateTimers();
        sourceTimers.TimerCounter = 0xFF;
        sourceTimers.TimerModulo = 0x42;
        sourceTimers.SetTimerControlState(0b0000_0101);
        TickMachineCycles(sourceCounter, sourceTimers, 5);

        var interrupts = new InterruptController();
        var (_, restoredTimers) = CreateTimers(interrupts);
        restoredTimers.RestoreState(sourceTimers.CaptureState());

        Assert.Equal(0x00, interrupts.InterruptFlag);
        restoredTimers.WriteTimerCounter(0x99);
        Assert.Equal(0x42, restoredTimers.TimerCounter);

        restoredTimers.AdvanceOverflowReload();
        restoredTimers.WriteTimerCounter(0x99);

        Assert.Equal(0x99, restoredTimers.TimerCounter);
    }

    private static (SystemCounter Counter, TimerController Timers) CreateTimers(
        InterruptController? interrupts = null,
        bool ticksOnTacDisableWhenInputHigh = true,
        bool ticksOnTacEnableWhenInputHigh = false
    )
    {
        var counter = new SystemCounter();
        return (
            counter,
            new TimerController(
                interrupts ?? new InterruptController(),
                counter,
                ticksOnTacDisableWhenInputHigh,
                ticksOnTacEnableWhenInputHigh
            )
        );
    }

    private static void ResetSystemCounter(SystemCounter counter, TimerController timers)
    {
        timers.TickSystemCounter(counter.Reset());
    }

    private static void TickMachineCycles(
        SystemCounter counter,
        TimerController timers,
        int machineCycles
    )
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            timers.AdvanceOverflowReload();
            timers.TickSystemCounter(counter.AdvanceMachineCycle());
        }
    }
}
