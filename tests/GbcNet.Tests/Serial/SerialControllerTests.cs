// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;

namespace GbcNet.Tests.Serial;

public sealed class SerialControllerTests
{
    [Fact]
    public void WriteControl_StoresUsefulDmgBitsAndReadsUnusedBitsSet()
    {
        var serial = new SerialController(new InterruptController());

        serial.WriteControl(0x81);

        Assert.Equal(0xFF, serial.ReadControl());
    }

    [Fact]
    public void WriteControl_InCgbModeStoresHighSpeedBit()
    {
        var serial = new SerialController(new InterruptController(), isHighSpeedClockEnabled: true);

        serial.WriteControl(0x81);
        Assert.Equal(0xFD, serial.ReadControl());

        serial.WriteControl(0x83);
        Assert.Equal(0xFF, serial.ReadControl());
    }

    [Fact]
    public void WriteControl_InDmgModeIgnoresHighSpeedBit()
    {
        var counter = new SystemCounter();
        var serial = new SerialController(new InterruptController());
        serial.WriteControl(0x83);

        TickMachineCycles(counter, serial, 32);

        Assert.NotEqual(0, serial.ReadControl() & 0x80);
        Assert.Equal(0x00, serial.TransferData);

        TickMachineCycles(counter, serial, 1024 - 32);

        Assert.Equal(0, serial.ReadControl() & 0x80);
        Assert.Equal(0xFF, serial.TransferData);
    }

    [Theory]
    [InlineData(false, false, 8192, 1024)]
    [InlineData(false, true, 262144, 32)]
    [InlineData(true, false, 16384, 1024)]
    [InlineData(true, true, 524288, 32)]
    public void TickMachineCycle_CompletesCgbInternalClockTransferAtPanDocsRate(
        bool doubleSpeed,
        bool highSpeed,
        int serialHz,
        int expectedMachineCycles
    )
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts, isHighSpeedClockEnabled: true);
        var clock = new ClockController(
            interrupts,
            serial,
            new ApuController(ApuModelSpec.Cgb),
            isKey1RegisterEnabled: true
        );
        if (doubleSpeed)
        {
            clock.WriteKey1(0x01);
            Assert.True(clock.TryStartSpeedSwitch());
        }

        serial.WriteControl((byte)(0x81 | (highSpeed ? 0x02 : 0x00)));

        TickMachineCycles(clock, expectedMachineCycles - 1);
        Assert.NotEqual(0, serial.ReadControl() & 0x80);

        TickMachineCycles(clock, 1);

        Assert.Equal(0, serial.ReadControl() & 0x80);
        Assert.Equal(0xFF, serial.TransferData);
        Assert.Equal(
            expectedMachineCycles,
            (doubleSpeed ? GameBoyTiming.DoubleCpuHz : GameBoyTiming.NormalCpuHz) / serialHz * 8
        );
    }

    [Fact]
    public void WriteControl_WhenMasterClockIsHigh_DelaysFirstShiftUntilNextLowEdge()
    {
        var counter = new SystemCounter();
        var serial = new SerialController(new InterruptController());
        TickMachineCycles(counter, serial, 64);

        serial.WriteControl(0x81);
        TickMachineCycles(counter, serial, 64);
        Assert.Equal(0x00, serial.TransferData);

        TickMachineCycles(counter, serial, 64);

        Assert.Equal(0x01, serial.TransferData);
    }

    [Fact]
    public void TickSystemCounter_CompletesInternalClockTransferAndRequestsSerialInterrupt()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += (_, e) => transferredByte = e.TransferredByte;
        serial.TransferData = 0x41;
        serial.WriteControl(0x81);
        serial.TransferData = 0x00;

        TickMachineCycles(counter, serial, 128 * 8);

        Assert.Equal(0xFF, serial.TransferData);
        Assert.Equal(0x7F, serial.ReadControl());
        Assert.Equal(0b0000_1000, interrupts.InterruptFlag);
        Assert.Equal((byte)0x41, transferredByte);
    }

    [Fact]
    public void TickSystemCounter_DoesNotAdvanceExternalClockTransfer()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += (_, e) => transferredByte = e.TransferredByte;
        serial.WriteControl(0x80);

        TickMachineCycles(counter, serial, 128 * 8);

        Assert.Equal(0x00, serial.TransferData);
        Assert.Equal(0xFE, serial.ReadControl());
        Assert.Equal(0x00, interrupts.InterruptFlag);
        Assert.Null(transferredByte);
    }

    [Fact]
    public void SetControlState_DoesNotStartTransfer()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += (_, e) => transferredByte = e.TransferredByte;
        serial.SetControlState(0x81);

        TickMachineCycles(counter, serial, 128 * 8);

        Assert.Equal(0x00, serial.TransferData);
        Assert.Equal(0xFF, serial.ReadControl());
        Assert.Equal(0x00, interrupts.InterruptFlag);
        Assert.Null(transferredByte);
    }

    private static void TickMachineCycles(ClockController clock, int machineCycles)
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            clock.TickMachineCycle();
        }
    }

    private static void TickMachineCycles(
        SystemCounter counter,
        SerialController serial,
        int machineCycles
    )
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            serial.TickSystemCounter(counter.AdvanceMachineCycle());
        }
    }
}
