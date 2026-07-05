// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Sm83;

public sealed class JumpInstructionTests
{
    [Fact]
    public void Step_JumpsToImmediate16Address()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xC3;
            bytes[0x0101] = 0x78;
            bytes[0x0102] = 0x56;
        });
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(0x5678, cpu.Registers.PC);
        Assert.Equal(0xF0, cpu.Registers.F);
    }

    [Theory]
    [InlineData(0xC2, 0x00, true)]
    [InlineData(0xC2, 0x80, false)]
    [InlineData(0xCA, 0x80, true)]
    [InlineData(0xCA, 0x00, false)]
    [InlineData(0xD2, 0x00, true)]
    [InlineData(0xD2, 0x10, false)]
    [InlineData(0xDA, 0x10, true)]
    [InlineData(0xDA, 0x00, false)]
    public void Step_ConditionallyJumpsToImmediate16Address(byte opcode, byte flags, bool isTaken)
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = 0x78;
            bytes[0x0102] = 0x56;
        });
        cpu.Registers.F = flags;

        var machineCycles = cpu.Step();

        Assert.Equal(isTaken ? 4 : 3, machineCycles);
        Assert.Equal(isTaken ? 0x5678 : 0x0103, cpu.Registers.PC);
        Assert.Equal(flags, cpu.Registers.F);
    }

    [Fact]
    public void Step_JumpsToHlAddress()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xE9);
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0xC123, cpu.Registers.PC);
        Assert.Equal(0xF0, cpu.Registers.F);
    }

    [Theory]
    [InlineData(0x02, 0x0104)]
    [InlineData(0xFE, 0x0100)]
    [InlineData(0x80, 0x0082)]
    public void Step_JumpsRelativeToNextInstruction(byte offset, ushort expectedPc)
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x18;
            bytes[0x0101] = offset;
        });
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(expectedPc, cpu.Registers.PC);
        Assert.Equal(0xF0, cpu.Registers.F);
    }

    [Theory]
    [InlineData(0x20, 0x00, 0x02, 0x0104, 3)]
    [InlineData(0x20, 0x80, 0x02, 0x0102, 2)]
    [InlineData(0x28, 0x80, 0xFE, 0x0100, 3)]
    [InlineData(0x28, 0x00, 0xFE, 0x0102, 2)]
    [InlineData(0x30, 0x00, 0x02, 0x0104, 3)]
    [InlineData(0x30, 0x10, 0x02, 0x0102, 2)]
    [InlineData(0x38, 0x10, 0xFE, 0x0100, 3)]
    [InlineData(0x38, 0x00, 0xFE, 0x0102, 2)]
    public void Step_ConditionallyJumpsRelativeToNextInstruction(
        byte opcode,
        byte flags,
        byte offset,
        ushort expectedPc,
        int expectedMachineCycles
    )
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = offset;
        });
        cpu.Registers.F = flags;

        var machineCycles = cpu.Step();

        Assert.Equal(expectedMachineCycles, machineCycles);
        Assert.Equal(expectedPc, cpu.Registers.PC);
        Assert.Equal(flags, cpu.Registers.F);
    }
}
