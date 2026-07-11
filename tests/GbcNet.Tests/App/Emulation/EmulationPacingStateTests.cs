// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using GbcNet.App.Emulation;

namespace GbcNet.Tests.App.EmulationState;

public sealed class EmulationPacingStateTests
{
    [Fact]
    public void Constructor_SchedulesFirstThrottleWindow()
    {
        var state = new EmulationPacingState(
            timestamp: 0,
            elapsedMachineCycles: 200,
            speedMultiplier: 1,
            cpuHz: 1000,
            revision: 0
        );

        Assert.Equal(208, state.NextThrottleMachineCycles);
        Assert.False(state.ShouldThrottle(elapsedMachineCycles: 207));
        Assert.True(state.ShouldThrottle(elapsedMachineCycles: 208));
    }

    [Fact]
    public void ScheduleNextThrottle_UsesAtLeastOneMachineCycle()
    {
        var state = new EmulationPacingState(
            timestamp: 0,
            elapsedMachineCycles: 10,
            speedMultiplier: 0.1,
            cpuHz: 1,
            revision: 0
        );

        Assert.Equal(11, state.NextThrottleMachineCycles);

        state.ScheduleNextThrottle(elapsedMachineCycles: 20);

        Assert.Equal(21, state.NextThrottleMachineCycles);
    }

    [Fact]
    public void GetDelayTimestamp_UsesBaseTimestampElapsedCyclesAndSpeed()
    {
        const int cpuHz = 1000;
        var state = new EmulationPacingState(
            timestamp: 100,
            elapsedMachineCycles: 10,
            speedMultiplier: 1,
            cpuHz,
            revision: 0
        );
        var expectedTimestamp =
            100
            + (long)Math.Round(5 * (Stopwatch.Frequency / (double)cpuHz), MidpointRounding.ToEven);

        var delay = state.GetDelayTimestamp(timestamp: 123, elapsedMachineCycles: 15);

        Assert.Equal(expectedTimestamp - 123, delay);
    }

    [Fact]
    public void ResetIfChanged_ReturnsFalseWhenPacingInputsAreUnchanged()
    {
        var state = new EmulationPacingState(
            timestamp: 10,
            elapsedMachineCycles: 20,
            speedMultiplier: 1.5,
            cpuHz: 1000,
            revision: 3
        );
        var nextThrottleMachineCycles = state.NextThrottleMachineCycles;

        var changed = state.ResetIfChanged(
            timestamp: 999,
            elapsedMachineCycles: 999,
            speedMultiplier: 1.5,
            cpuHz: 1000,
            revision: 3
        );

        Assert.False(changed);
        Assert.Equal(1.5, state.SpeedMultiplier);
        Assert.Equal(nextThrottleMachineCycles, state.NextThrottleMachineCycles);
    }

    [Fact]
    public void ResetIfChanged_ResetsWhenPacingInputChanges()
    {
        var state = new EmulationPacingState(
            timestamp: 0,
            elapsedMachineCycles: 0,
            speedMultiplier: 1,
            cpuHz: 1000,
            revision: 1
        );

        var changed = state.ResetIfChanged(
            timestamp: 50,
            elapsedMachineCycles: 100,
            speedMultiplier: 2,
            cpuHz: 1000,
            revision: 1
        );

        Assert.True(changed);
        Assert.Equal(2, state.SpeedMultiplier);
        Assert.Equal(116, state.NextThrottleMachineCycles);
        Assert.Equal(0, state.GetDelayTimestamp(timestamp: 50, elapsedMachineCycles: 100));
    }

    [Fact]
    public void RebaseIfTooLate_PreservesBoundedCatchUpDebt()
    {
        const int cpuHz = 1000;
        var state = new EmulationPacingState(0, 0, 1, cpuHz, revision: 0);
        var expectedTimestamp = (long)
            Math.Round(8 * (Stopwatch.Frequency / (double)cpuHz), MidpointRounding.ToEven);
        var timestamp = expectedTimestamp + (Stopwatch.Frequency * 10 / 1000);

        var rebased = state.RebaseIfTooLate(timestamp, elapsedMachineCycles: 8);

        Assert.False(rebased);
        Assert.True(state.GetDelayTimestamp(timestamp, elapsedMachineCycles: 8) < 0);
    }

    [Fact]
    public void RebaseIfTooLate_DropsExcessDebtAndStartsNextWindowFromNow()
    {
        const int cpuHz = 1000;
        var state = new EmulationPacingState(0, 0, 1, cpuHz, revision: 0);
        var firstWindowTimestamp = (long)
            Math.Round(8 * (Stopwatch.Frequency / (double)cpuHz), MidpointRounding.ToEven);
        var timestamp = firstWindowTimestamp + (Stopwatch.Frequency * 100 / 1000);

        var rebased = state.RebaseIfTooLate(timestamp, elapsedMachineCycles: 8);
        state.ScheduleNextThrottle(elapsedMachineCycles: 8);

        Assert.True(rebased);
        Assert.Equal(0, state.GetDelayTimestamp(timestamp, elapsedMachineCycles: 8));
        Assert.Equal(16, state.NextThrottleMachineCycles);
        Assert.Equal(
            firstWindowTimestamp,
            state.GetDelayTimestamp(timestamp, elapsedMachineCycles: 16)
        );
    }
}
