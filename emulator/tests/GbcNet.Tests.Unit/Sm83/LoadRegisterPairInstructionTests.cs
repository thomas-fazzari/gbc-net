// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.Sm83;

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

        machineCycles.Should().Be(3);
        cpu.Registers.HL.Should().Be(expectedHl);
        cpu.Registers.SP.Should().Be(stackPointer);
        cpu.Registers.F.Should().Be(expectedFlags);
        cpu.Registers.PC.Should().Be(0x0102);
    }

    [Fact]
    public void Step_LoadsStackPointerFromHlWithoutChangingFlags()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xF9);
        cpu.Registers.HL = 0xC123;
        cpu.Registers.SP = 0xFFFE;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(2);
        cpu.Registers.SP.Should().Be(0xC123);
        cpu.Registers.HL.Should().Be(0xC123);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0101);
    }
}
