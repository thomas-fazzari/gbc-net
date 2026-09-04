// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;

namespace GbcNet.Tests.Unit.Serial;

public sealed class SerialControllerTests
{
    [Fact]
    public void WriteControl_StoresUsefulDmgBitsAndReadsUnusedBitsSet()
    {
        var serial = new SerialController(new InterruptController());

        serial.WriteControl(0x81);

        serial.ReadControl().Should().Be(0xFF);
    }

    [Fact]
    public void WriteControl_InCgbModeStoresHighSpeedBit()
    {
        var serial = new SerialController(new InterruptController(), isHighSpeedClockEnabled: true);

        serial.WriteControl(0x81);
        serial.ReadControl().Should().Be(0xFD);

        serial.WriteControl(0x83);
        serial.ReadControl().Should().Be(0xFF);
    }

    [Fact]
    public void WriteControl_InDmgModeIgnoresHighSpeedBit()
    {
        var counter = new SystemCounter();
        var serial = new SerialController(new InterruptController());
        serial.WriteControl(0x83);

        TickMachineCycles(counter, serial, 32);

        (serial.ReadControl() & 0x80).Should().NotBe(0);
        serial.TransferData.Should().Be(0x00);

        TickMachineCycles(counter, serial, 1024 - 32);

        (serial.ReadControl() & 0x80).Should().Be(0);
        serial.TransferData.Should().Be(0xFF);
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
            clock.TryStartSpeedSwitch().Should().BeTrue();
        }

        serial.WriteControl((byte)(0x81 | (highSpeed ? 0x02 : 0x00)));

        TickMachineCycles(clock, expectedMachineCycles - 1);
        (serial.ReadControl() & 0x80).Should().NotBe(0);

        TickMachineCycles(clock, 1);

        (serial.ReadControl() & 0x80).Should().Be(0);
        serial.TransferData.Should().Be(0xFF);
        ((doubleSpeed ? GameBoyTiming.DoubleCpuHz : GameBoyTiming.NormalCpuHz) / serialHz * 8)
            .Should()
            .Be(expectedMachineCycles);
    }

    [Fact]
    public void WriteControl_WhenMasterClockIsHigh_DelaysFirstShiftUntilNextLowEdge()
    {
        var counter = new SystemCounter();
        var serial = new SerialController(new InterruptController());
        TickMachineCycles(counter, serial, 64);

        serial.WriteControl(0x81);
        TickMachineCycles(counter, serial, 64);
        serial.TransferData.Should().Be(0x00);

        TickMachineCycles(counter, serial, 64);

        serial.TransferData.Should().Be(0x01);
    }

    [Fact]
    public void TickSystemCounter_CompletesInternalClockTransferAndRequestsSerialInterrupt()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += transferredByteValue => transferredByte = transferredByteValue;
        serial.TransferData = 0x41;
        serial.WriteControl(0x81);
        serial.TransferData = 0x00;

        TickMachineCycles(counter, serial, 128 * 8);

        serial.TransferData.Should().Be(0xFF);
        serial.ReadControl().Should().Be(0x7F);
        interrupts.InterruptFlag.Should().Be(0b0000_1000);
        transferredByte.Should().Be(0x41);
    }

    [Fact]
    public void TickSystemCounter_DoesNotAdvanceExternalClockTransfer()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += transferredByteValue => transferredByte = transferredByteValue;
        serial.WriteControl(0x80);

        TickMachineCycles(counter, serial, 128 * 8);

        serial.TransferData.Should().Be(0x00);
        serial.ReadControl().Should().Be(0xFE);
        interrupts.InterruptFlag.Should().Be(0x00);
        transferredByte.Should().BeNull();
    }

    [Fact]
    public void SetControlState_DoesNotStartTransfer()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += transferredByteValue => transferredByte = transferredByteValue;
        serial.SetControlState(0x81);

        TickMachineCycles(counter, serial, 128 * 8);

        serial.TransferData.Should().Be(0x00);
        serial.ReadControl().Should().Be(0xFF);
        interrupts.InterruptFlag.Should().Be(0x00);
        transferredByte.Should().BeNull();
    }

    [Theory]
    [InlineData(false, 576, 448)]
    [InlineData(true, 18, 14)]
    public void RestoreState_ResumesMidBitTransferAtExactRemainingTiming(
        bool highSpeed,
        int snapshotMachineCycles,
        int remainingMachineCycles
    )
    {
        var sourceInterrupts = new InterruptController();
        var sourceSerial = new SerialController(sourceInterrupts, isHighSpeedClockEnabled: true);
        var sourceClock = new ClockController(
            sourceInterrupts,
            sourceSerial,
            new ApuController(ApuModelSpec.Cgb),
            isKey1RegisterEnabled: true
        );
        sourceSerial.TransferData = 0xA5;
        sourceSerial.WriteControl((byte)(0x81 | (highSpeed ? 0x02 : 0x00)));
        sourceSerial.TransferData = 0x00;
        TickMachineCycles(sourceClock, snapshotMachineCycles);

        var clockState = sourceClock.CaptureState();
        var serialState = sourceSerial.CaptureState();

        var restoredInterrupts = new InterruptController();
        var restoredSerial = new SerialController(
            restoredInterrupts,
            isHighSpeedClockEnabled: true
        );
        var restoredClock = new ClockController(
            restoredInterrupts,
            restoredSerial,
            new ApuController(ApuModelSpec.Cgb),
            isKey1RegisterEnabled: true
        );
        var transferredBytes = new List<byte>();
        restoredSerial.ByteTransferred += transferredBytes.Add;

        restoredClock.RestoreState(clockState);
        restoredSerial.RestoreState(serialState);

        transferredBytes.Should().BeEmpty();
        restoredInterrupts.InterruptFlag.Should().Be(0x00);

        TickMachineCycles(restoredClock, remainingMachineCycles - 1);
        (restoredSerial.ReadControl() & 0x80).Should().NotBe(0);
        transferredBytes.Should().BeEmpty();

        TickMachineCycles(restoredClock, 1);

        restoredSerial.TransferData.Should().Be(0xFF);
        (restoredSerial.ReadControl() & 0x80).Should().Be(0);
        restoredInterrupts.InterruptFlag.Should().Be(0b0000_1000);
        transferredBytes.Should().Equal(0xA5);
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
