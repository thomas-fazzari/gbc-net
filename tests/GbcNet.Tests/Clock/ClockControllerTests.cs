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

    private static ClockController CreateClock(bool isKey1RegisterEnabled)
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        var apu = new ApuController(ApuModelSpec.Dmg);
        return new ClockController(interrupts, serial, apu, isKey1RegisterEnabled);
    }
}
