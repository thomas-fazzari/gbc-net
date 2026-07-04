// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Sm83;

public sealed class LoadRegisterPairInstructionTests
{
    [Theory]
    [InlineData(0x1234, 0x02, 0x1236, 0x00)]
    [InlineData(0x120F, 0x01, 0x1210, 0x20)]
    [InlineData(0x12FF, 0x01, 0x1300, 0x30)]
    [InlineData(0x1234, 0xFF, 0x1233, 0x30)]
    [InlineData(0x0100, 0x80, 0x0080, 0x00)]
    public void Step_LoadsHlFromStackPointerPlusSignedImmediate8AndUpdatesFlags(
        ushort stackPointer,
        byte offset,
        ushort expectedHl,
        byte expectedFlags
    )
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xF8;
            bytes[0x0101] = offset;
        });
        cpu.Registers.SP = stackPointer;
        cpu.Registers.HL = 0xFFFF;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(expectedHl, cpu.Registers.HL);
        Assert.Equal(stackPointer, cpu.Registers.SP);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsStackPointerFromHlWithoutChangingFlags()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xF9);
        cpu.Registers.HL = 0xC123;
        cpu.Registers.SP = 0xFFFE;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0xC123, cpu.Registers.SP);
        Assert.Equal(0xC123, cpu.Registers.HL);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }
}
