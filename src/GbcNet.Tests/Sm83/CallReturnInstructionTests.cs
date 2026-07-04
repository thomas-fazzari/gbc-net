// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Sm83;

public sealed class CallReturnInstructionTests
{
    private const byte VBlankInterrupt = 0b0000_0001;
    private const ushort VBlankVector = 0x0040;

    [Fact]
    public void Step_CallsImmediate16AndPushesReturnAddress()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCD;
            bytes[0x0101] = 0x34;
            bytes[0x0102] = 0x12;
        });
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(6, machineCycles);
        Assert.Equal(0x1234, cpu.Registers.PC);
        Assert.Equal(0xC0FE, cpu.Registers.SP);
        Assert.Equal(0x03, CpuTestFactory.GetBus(cpu).ReadByte(0xC0FE));
        Assert.Equal(0x01, CpuTestFactory.GetBus(cpu).ReadByte(0xC0FF));
        Assert.Equal(0xF0, cpu.Registers.F);
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
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = 0x78;
            bytes[0x0102] = 0x56;
        });
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = flags;

        var machineCycles = cpu.Step();

        Assert.Equal(isTaken ? 6 : 3, machineCycles);
        Assert.Equal(isTaken ? 0x5678 : 0x0103, cpu.Registers.PC);
        Assert.Equal(isTaken ? 0xC0FE : 0xC100, cpu.Registers.SP);
        Assert.Equal(flags, cpu.Registers.F);

        if (!isTaken)
        {
            return;
        }

        Assert.Equal(0x03, CpuTestFactory.GetBus(cpu).ReadByte(0xC0FE));
        Assert.Equal(0x01, CpuTestFactory.GetBus(cpu).ReadByte(0xC0FF));
    }

    [Fact]
    public void Step_ReturnsToStackAddress()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xC9);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;
        CpuTestFactory.GetBus(cpu).WriteByte(0xC100, 0x78);
        CpuTestFactory.GetBus(cpu).WriteByte(0xC101, 0x56);

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(0x5678, cpu.Registers.PC);
        Assert.Equal(0xC102, cpu.Registers.SP);
        Assert.Equal(0xF0, cpu.Registers.F);
    }

    [Fact]
    public void Step_ReturnsFromInterruptAndEnablesInterruptMasterEnableImmediately()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0xD9;
        });
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;
        CpuTestFactory.GetBus(cpu).WriteByte(0xC100, 0x78);
        CpuTestFactory.GetBus(cpu).WriteByte(0xC101, 0x56);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.ImeEnablePending);

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(0x5678, cpu.Registers.PC);
        Assert.Equal(0xC102, cpu.Registers.SP);
        Assert.True(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0xF0, cpu.Registers.F);
    }

    [Fact]
    public void Step_ServicesPendingInterruptOnStepAfterReturnFromInterrupt()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xD9;
            bytes[0x5678] = 0x00;
        });
        cpu.Registers.SP = 0xC100;
        CpuTestFactory.GetBus(cpu).WriteByte(0xC100, 0x78);
        CpuTestFactory.GetBus(cpu).WriteByte(0xC101, 0x56);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(4, cpu.Step());
        Assert.True(cpu.Ime);
        Assert.Equal(0x5678, cpu.Registers.PC);
        Assert.Equal(0xE1, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));

        Assert.Equal(5, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0xE0, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));
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
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = flags;
        CpuTestFactory.GetBus(cpu).WriteByte(0xC100, 0x78);
        CpuTestFactory.GetBus(cpu).WriteByte(0xC101, 0x56);

        var machineCycles = cpu.Step();

        Assert.Equal(isTaken ? 5 : 2, machineCycles);
        Assert.Equal(isTaken ? 0x5678 : 0x0101, cpu.Registers.PC);
        Assert.Equal(isTaken ? 0xC102 : 0xC100, cpu.Registers.SP);
        Assert.Equal(flags, cpu.Registers.F);
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
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(targetAddress, cpu.Registers.PC);
        Assert.Equal(0xC0FE, cpu.Registers.SP);
        Assert.Equal(0x01, CpuTestFactory.GetBus(cpu).ReadByte(0xC0FE));
        Assert.Equal(0x01, CpuTestFactory.GetBus(cpu).ReadByte(0xC0FF));
        Assert.Equal(0xF0, cpu.Registers.F);
    }
}
