// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges.Memory;

namespace GbcNet.Tests.Unit.Cartridges;

public sealed class Mbc3RealTimeClockTests
{
    [Fact]
    public void RestoreState_DoesNotCatchUpOfflineTime()
    {
        FakeClock clock = new() { UnixTimeSeconds = 10_000 };
        Mbc3RealTimeClock rtc = new(clock.Read);

        rtc.RestoreState(CreateState(seconds: 10));

        var state = rtc.CaptureState();

        state.Live.Seconds.Should().Be(10);
    }

    [Fact]
    public void RestoreState_ResumesOneSecondAfterItsNewClockAnchor()
    {
        FakeClock clock = new() { UnixTimeSeconds = 100 };
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10));
        clock.UnixTimeSeconds++;

        var state = rtc.CaptureState();

        state.Live.Seconds.Should().Be(11);
    }

    [Fact]
    public void RestoreState_RebasesHaltedClockUntilItIsUnhalted()
    {
        FakeClock clock = new() { UnixTimeSeconds = 100 };
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10, halted: true));
        clock.UnixTimeSeconds += 100;

        rtc.CaptureState().Live.Seconds.Should().Be(10);

        rtc.WriteRegister(Mbc3RealTimeClock.DayHighRegister, 0);
        rtc.ClearDirty();
        clock.UnixTimeSeconds++;

        rtc.CaptureState().Live.Seconds.Should().Be(11);
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

        rollover.Live.Day.Should().Be(0);
        rollover.Live.Carry.Should().BeTrue();
        followingDay.Live.Day.Should().Be(1);
        followingDay.Live.Carry.Should().BeTrue();
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

        firstCapture.Live.Seconds.Should().Be(15);
        firstCapture.Latched.Seconds.Should().Be(7);
        rtc.ReadRegister(Mbc3RealTimeClock.SecondsRegister).Should().Be(7);
        secondCapture.Live.Seconds.Should().Be(16);
    }

    [Fact]
    public void CaptureState_ReportsProjectedAndExistingDirtyStateExactly()
    {
        FakeClock clock = new();
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState());

        rtc.CaptureState().IsDirty.Should().BeFalse();

        clock.UnixTimeSeconds++;

        rtc.CaptureState().IsDirty.Should().BeTrue();
        rtc.IsDirty.Should().BeFalse();

        rtc.WriteRegister(Mbc3RealTimeClock.SecondsRegister, 9);

        rtc.CaptureState().IsDirty.Should().BeTrue();
    }

    [Fact]
    public void RestoreState_RejectsInvalidStateWithoutChangingClockOrRegisters()
    {
        FakeClock clock = new() { UnixTimeSeconds = 100 };
        Mbc3RealTimeClock rtc = new(clock.Read);
        rtc.RestoreState(CreateState(seconds: 10, latchedSeconds: 7));
        clock.ReadCount = 0;

        FluentActions
            .Invoking(() => rtc.RestoreState(CreateState(seconds: 64)))
            .Should()
            .ThrowExactly<ArgumentException>();
        clock.ReadCount.Should().Be(0);

        clock.UnixTimeSeconds++;
        var state = rtc.CaptureState();

        state.Live.Seconds.Should().Be(11);
        state.Latched.Seconds.Should().Be(7);
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
            new RtcRegisterSet(seconds, minutes, hours, day, halted, carry),
            new RtcRegisterSet(
                latchedSeconds,
                latchedMinutes,
                latchedHours,
                latchedDay,
                latchedHalted,
                latchedCarry
            ),
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
