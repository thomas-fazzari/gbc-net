// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc3RealTimeClockTests
{
    [Fact]
    public void RestoreState_DoesNotCatchUpOfflineTime()
    {
        FakeClock clock = new() { UnixTimeSeconds = 10_000 };
        Mbc3RealTimeClock rtc = new(clock.Read);

        rtc.RestoreState(CreateState(seconds: 10));

        var state = rtc.CaptureState();

        Assert.Equal(10, state.Seconds);
    }

    [Fact]
    public void RestoreState_ResumesOneSecondAfterItsNewClockAnchor()
    {
        FakeClock clock = new() { UnixTimeSeconds = 100 };
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10));
        clock.UnixTimeSeconds++;

        var state = rtc.CaptureState();

        Assert.Equal(11, state.Seconds);
    }

    [Fact]
    public void RestoreState_RebasesHaltedClockUntilItIsUnhalted()
    {
        FakeClock clock = new() { UnixTimeSeconds = 100 };
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10, halted: true));
        clock.UnixTimeSeconds += 100;

        Assert.Equal(10, rtc.CaptureState().Seconds);

        rtc.WriteRegister(Mbc3RealTimeClock.DayHighRegister, 0);
        rtc.ClearDirty();
        clock.UnixTimeSeconds++;

        Assert.Equal(11, rtc.CaptureState().Seconds);
    }

    // Pan Docs MBC3 "The Day Counter": carry remains set until explicitly reset.
    [Fact]
    public void CaptureState_PreservesStickyCarryAcrossDayRollover()
    {
        FakeClock clock = new();
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 59, minutes: 59, hours: 23, day: 511));
        clock.UnixTimeSeconds++;

        var rollover = rtc.CaptureState();
        clock.UnixTimeSeconds += 86_400;
        var followingDay = rtc.CaptureState();

        Assert.Equal(0, rollover.Day);
        Assert.True(rollover.Carry);
        Assert.Equal(1, followingDay.Day);
        Assert.True(followingDay.Carry);
    }

    [Fact]
    public void CaptureState_ProjectsLiveClockWithoutChangingLatchedRegisters()
    {
        FakeClock clock = new();
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10, latchedSeconds: 7));
        clock.UnixTimeSeconds += 5;

        var firstCapture = rtc.CaptureState();
        clock.UnixTimeSeconds++;
        var secondCapture = rtc.CaptureState();

        Assert.Equal(15, firstCapture.Seconds);
        Assert.Equal(7, firstCapture.LatchedSeconds);
        Assert.Equal(7, rtc.ReadRegister(Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(16, secondCapture.Seconds);
    }

    [Fact]
    public void CaptureState_ReportsProjectedAndExistingDirtyStateExactly()
    {
        FakeClock clock = new();
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState());

        Assert.False(rtc.CaptureState().IsDirty);

        clock.UnixTimeSeconds++;

        Assert.True(rtc.CaptureState().IsDirty);
        Assert.False(rtc.IsDirty);

        rtc.WriteRegister(Mbc3RealTimeClock.SecondsRegister, 9);

        Assert.True(rtc.CaptureState().IsDirty);
    }

    [Fact]
    public void RestoreState_RejectsInvalidStateWithoutChangingClockOrRegisters()
    {
        FakeClock clock = new() { UnixTimeSeconds = 100 };
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10, latchedSeconds: 7));
        clock.ReadCount = 0;

        Assert.Throws<ArgumentException>(() => rtc.RestoreState(CreateState(seconds: 64)));
        Assert.Equal(0, clock.ReadCount);

        clock.UnixTimeSeconds++;
        var state = rtc.CaptureState();

        Assert.Equal(11, state.Seconds);
        Assert.Equal(7, state.LatchedSeconds);
    }

    private static Mbc3RealTimeClockState CreateState(
        int seconds = 0,
        int minutes = 0,
        int hours = 0,
        int day = 0,
        bool halted = false,
        bool carry = false,
        int latchedSeconds = 0,
        int latchedMinutes = 0,
        int latchedHours = 0,
        int latchedDay = 0,
        bool latchedHalted = false,
        bool latchedCarry = false,
        bool isDirty = false
    ) =>
        new(
            seconds,
            minutes,
            hours,
            day,
            halted,
            carry,
            latchedSeconds,
            latchedMinutes,
            latchedHours,
            latchedDay,
            latchedHalted,
            latchedCarry,
            isDirty
        );

    private sealed class FakeClock
    {
        public long UnixTimeSeconds { get; set; }

        public int ReadCount { get; set; }

        public long Read()
        {
            ReadCount++;
            return UnixTimeSeconds;
        }
    }
}
