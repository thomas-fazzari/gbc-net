// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

public sealed class CallReturnInstructionTests
{
    private const byte VBlankInterrupt = 0b0000_0001;
    private const ushort VBlankVector = 0x0040;

    [Fact]
    public void Step_CallsImmediate16AndPushesReturnAddress()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xCD;
            bytes[0x0101] = 0x34;
            bytes[0x0102] = 0x12;
        });
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(6);
        cpu.Registers.PC.Should().Be(0x1234);
        cpu.Registers.SP.Should().Be(0xC0FE);
        bus.ReadByte(0xC0FE).Should().Be(0x03);
        bus.ReadByte(0xC0FF).Should().Be(0x01);
        cpu.Registers.F.Should().Be(0xF0);
    }

    [Theory]
    [InlineData(0xC4, 0x00, true)]
    [InlineData(0xC4, 0x80, false)]
    [InlineData(0xCC, 0x80, true)]
    [InlineData(0xCC, 0x00, false)]
    [InlineData(0xD4, 0x00, true)]
    [InlineData(0xD4, 0x10, false)]
    [InlineData(0xDC, 0x10, true)]
    [InlineData(0xDC, 0x00, false)]
    public void Step_ConditionallyCallsImmediate16(byte opcode, byte flags, bool isTaken)
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = 0x78;
            bytes[0x0102] = 0x56;
        });
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = flags;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(isTaken ? 6 : 3);
        cpu.Registers.PC.Should().Be(isTaken ? (ushort)0x5678 : (ushort)0x0103);
        cpu.Registers.SP.Should().Be(isTaken ? (ushort)0xC0FE : (ushort)0xC100);
        cpu.Registers.F.Should().Be(flags);

        if (!isTaken)
        {
            return;
        }

        bus.ReadByte(0xC0FE).Should().Be(0x03);
        bus.ReadByte(0xC0FF).Should().Be(0x01);
    }

    [Fact]
    public void Step_ReturnsToStackAddress()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0xC9);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;
        bus.WriteByte(0xC100, 0x78);
        bus.WriteByte(0xC101, 0x56);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        cpu.Registers.PC.Should().Be(0x5678);
        cpu.Registers.SP.Should().Be(0xC102);
        cpu.Registers.F.Should().Be(0xF0);
    }

    // Pan Docs `interrupts.md`: RETI enables IME immediately.
    [Fact]
    public void Step_ReturnsFromInterruptAndEnablesInterruptMasterEnableImmediately()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0xD9;
        });
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;
        bus.WriteByte(0xC100, 0x78);
        bus.WriteByte(0xC101, 0x56);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.EnablePending);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        cpu.Registers.PC.Should().Be(0x5678);
        cpu.Registers.SP.Should().Be(0xC102);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.Registers.F.Should().Be(0xF0);
    }

    [Fact]
    public void Step_ServicesPendingInterruptOnStepAfterReturnFromInterrupt()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xD9;
            bytes[0x5678] = 0x00;
        });
        cpu.Registers.SP = 0xC100;
        bus.WriteByte(0xC100, 0x78);
        bus.WriteByte(0xC101, 0x56);
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(4);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.Registers.PC.Should().Be(0x5678);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);

        cpu.Step().Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(VBlankVector);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Theory]
    [InlineData(0xC0, 0x00, true)]
    [InlineData(0xC0, 0x80, false)]
    [InlineData(0xC8, 0x80, true)]
    [InlineData(0xC8, 0x00, false)]
    [InlineData(0xD0, 0x00, true)]
    [InlineData(0xD0, 0x10, false)]
    [InlineData(0xD8, 0x10, true)]
    [InlineData(0xD8, 0x00, false)]
    public void Step_ConditionallyReturns(byte opcode, byte flags, bool isTaken)
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = flags;
        bus.WriteByte(0xC100, 0x78);
        bus.WriteByte(0xC101, 0x56);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(isTaken ? 5 : 2);
        cpu.Registers.PC.Should().Be(isTaken ? (ushort)0x5678 : (ushort)0x0101);
        cpu.Registers.SP.Should().Be(isTaken ? (ushort)0xC102 : (ushort)0xC100);
        cpu.Registers.F.Should().Be(flags);
    }

    [Theory]
    [InlineData(0xC7, 0x0000)]
    [InlineData(0xCF, 0x0008)]
    [InlineData(0xD7, 0x0010)]
    [InlineData(0xDF, 0x0018)]
    [InlineData(0xE7, 0x0020)]
    [InlineData(0xEF, 0x0028)]
    [InlineData(0xF7, 0x0030)]
    [InlineData(0xFF, 0x0038)]
    public void Step_RestartsAtEncodedVector(byte opcode, ushort targetAddress)
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        cpu.Registers.PC.Should().Be(targetAddress);
        cpu.Registers.SP.Should().Be(0xC0FE);
        bus.ReadByte(0xC0FE).Should().Be(0x01);
        bus.ReadByte(0xC0FF).Should().Be(0x01);
        cpu.Registers.F.Should().Be(0xF0);
    }
}
