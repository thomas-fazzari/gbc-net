// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;

namespace GbcNet.Tests.Clock;

public sealed class ClockControllerTests
{
    [Fact]
    public void ReadWriteKey1_StoresOnlyArmedBitAndReadsUnusedBitsHigh()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);

        Assert.Equal(0x7E, clock.ReadKey1());

        clock.WriteKey1(0xFF);

        Assert.Equal(0x7F, clock.ReadKey1());

        clock.WriteKey1(0xFE);

        Assert.Equal(0x7E, clock.ReadKey1());
    }

    [Fact]
    public void TryStartSpeedSwitch_TogglesSpeedResetsDividerClearsArmedBitAndStartsPause()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);
        clock.SetDivider(0x12);
        clock.WriteKey1(0x01);

        Assert.True(clock.TryStartSpeedSwitch());

        Assert.True(clock.CgbDoubleSpeed);
        Assert.Equal(0xFE, clock.ReadKey1());
        Assert.Equal(0x00, clock.ReadDivider());
        Assert.Equal(
            HardwareTiming.DoubleSpeedMachineCycleTCycles,
            clock.VideoAndAudioTCyclesPerMachineCycle
        );
        Assert.Equal(2050, clock.SpeedSwitchPauseCycles);
    }

    [Fact]
    public void TryStartSpeedSwitch_ReturnsFalseWhenKey1IsNotArmed()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);

        Assert.False(clock.TryStartSpeedSwitch());

        Assert.False(clock.CgbDoubleSpeed);
        Assert.Equal(0x7E, clock.ReadKey1());
    }

    [Fact]
    public void ReadWriteKey1_IgnoresDisabledRegister()
    {
        var clock = CreateClock(isKey1RegisterEnabled: false);

        clock.WriteKey1(0x01);

        Assert.Equal(0xFF, clock.ReadKey1());
        Assert.False(clock.TryStartSpeedSwitch());
        Assert.False(clock.CgbDoubleSpeed);
    }

    [Fact]
    public void CaptureRestoreState_RestoresRawDividerPhaseAndNestedTimerAtNextFallingEdge()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);
        clock.SetCounter(0x00FC);
        clock.Timers.TimerCounter = 0x3A;
        clock.Timers.TimerModulo = 0x6D;
        clock.Timers.SetTimerControlState(0b0000_0101);
        var state = clock.CaptureState();

        clock.SetCounter(0);
        clock.Timers.TimerCounter = 0;
        clock.Timers.TimerModulo = 0;
        clock.Timers.SetTimerControlState(0);
        clock.RestoreState(state);
        clock.TickMachineCycle();

        Assert.Equal(0x01, clock.ReadDivider());
        Assert.Equal(0x3B, clock.Timers.TimerCounter);
        Assert.Equal(0x6D, clock.Timers.TimerModulo);
    }

    [Fact]
    public void CaptureRestoreState_RestoresDoubleSpeedArmingAndRemainingPause()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);
        clock.WriteKey1(0x01);
        Assert.True(clock.TryStartSpeedSwitch());

        for (var cycle = 0; cycle < 6; cycle++)
        {
            Assert.True(clock.TryStepSpeedSwitchPause());
        }

        clock.WriteKey1(0x01);
        var state = clock.CaptureState();

        for (var cycle = 0; cycle < 2044; cycle++)
        {
            Assert.True(clock.TryStepSpeedSwitchPause());
        }

        clock.SetKey1State(0);
        clock.RestoreState(state);

        Assert.True(clock.CgbDoubleSpeed);
        Assert.Equal(0xFF, clock.ReadKey1());
        Assert.Equal(2044, clock.SpeedSwitchPauseCycles);
        Assert.True(clock.TryStepSpeedSwitchPause());
        Assert.Equal(2043, clock.SpeedSwitchPauseCycles);
    }

    [Fact]
    public void RestoreState_RejectsKey1StateWhenRegisterIsDisabled()
    {
        var clock = CreateClock(isKey1RegisterEnabled: false);
        var state = clock.CaptureState() with
        {
            CgbDoubleSpeed = true,
            SpeedSwitchArmed = true,
            SpeedSwitchPauseCycles = 1,
        };

        Assert.Throws<ArgumentException>(() => clock.RestoreState(state));
    }

    private static ClockController CreateClock(bool isKey1RegisterEnabled)
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        var apu = new ApuController(ApuModelSpec.Dmg);
        return new ClockController(interrupts, serial, apu, isKey1RegisterEnabled);
    }
}
