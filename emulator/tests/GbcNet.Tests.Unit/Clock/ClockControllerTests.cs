// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;

namespace GbcNet.Tests.Unit.Clock;

public sealed class ClockControllerTests
{
    [Fact]
    public void ReadWriteKey1_StoresOnlyArmedBitAndReadsUnusedBitsHigh()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);

        clock.ReadKey1().Should().Be(0x7E);

        clock.WriteKey1(0xFF);

        clock.ReadKey1().Should().Be(0x7F);

        clock.WriteKey1(0xFE);

        clock.ReadKey1().Should().Be(0x7E);
    }

    [Fact]
    public void TryStartSpeedSwitch_TogglesSpeedResetsDividerClearsArmedBitAndStartsPause()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);
        clock.SetDivider(0x12);
        clock.WriteKey1(0x01);

        clock.TryStartSpeedSwitch().Should().BeTrue();

        clock.CgbDoubleSpeed.Should().BeTrue();
        clock.ReadKey1().Should().Be(0xFE);
        clock.ReadDivider().Should().Be(0x00);
        clock
            .VideoAndAudioTCyclesPerMachineCycle.Should()
            .Be(HardwareTiming.DoubleSpeedMachineCycleTCycles);
        clock.SpeedSwitchPauseCycles.Should().Be(ClockController.SpeedSwitchPauseDuration);
    }

    [Fact]
    public void TryStartSpeedSwitch_ReturnsFalseWhenKey1IsNotArmed()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);

        clock.TryStartSpeedSwitch().Should().BeFalse();

        clock.CgbDoubleSpeed.Should().BeFalse();
        clock.ReadKey1().Should().Be(0x7E);
    }

    [Fact]
    public void ReadWriteKey1_IgnoresDisabledRegister()
    {
        var clock = CreateClock(isKey1RegisterEnabled: false);

        clock.WriteKey1(0x01);

        clock.ReadKey1().Should().Be(0xFF);
        clock.TryStartSpeedSwitch().Should().BeFalse();
        clock.CgbDoubleSpeed.Should().BeFalse();
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

        clock.ReadDivider().Should().Be(0x01);
        clock.Timers.TimerCounter.Should().Be(0x3B);
        clock.Timers.TimerModulo.Should().Be(0x6D);
    }

    [Fact]
    public void CaptureRestoreState_RestoresDoubleSpeedArmingAndRemainingPause()
    {
        var clock = CreateClock(isKey1RegisterEnabled: true);
        clock.WriteKey1(0x01);
        clock.TryStartSpeedSwitch().Should().BeTrue();

        for (var cycle = 0; cycle < 6; cycle++)
        {
            clock.TryStepSpeedSwitchPause().Should().BeTrue();
        }

        clock.WriteKey1(0x01);
        var state = clock.CaptureState();

        for (var cycle = 0; cycle < 2044; cycle++)
        {
            clock.TryStepSpeedSwitchPause().Should().BeTrue();
        }

        clock.SetKey1State(0);
        clock.RestoreState(state);

        clock.CgbDoubleSpeed.Should().BeTrue();
        clock.ReadKey1().Should().Be(0xFF);
        clock.SpeedSwitchPauseCycles.Should().Be(2044);
        clock.TryStepSpeedSwitchPause().Should().BeTrue();
        clock.SpeedSwitchPauseCycles.Should().Be(2043);
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

        FluentActions
            .Invoking(() => clock.RestoreState(state))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    private static ClockController CreateClock(bool isKey1RegisterEnabled)
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        var apu = new ApuController(ApuModelSpec.Dmg);
        return new ClockController(interrupts, serial, apu, isKey1RegisterEnabled);
    }
}
