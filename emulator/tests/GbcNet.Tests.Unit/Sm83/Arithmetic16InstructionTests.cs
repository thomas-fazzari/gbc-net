// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.Sm83;

public sealed class Arithmetic16InstructionTests
{
    [Theory]
    [InlineData(0x1234, 0x02, 0x1236, 0x00)]
    [InlineData(0x120F, 0x01, 0x1210, 0x20)]
    [InlineData(0x12FF, 0x01, 0x1300, 0x30)]
    [InlineData(0x1234, 0xFF, 0x1233, 0x30)]
    [InlineData(0x0100, 0x80, 0x0080, 0x00)]
    public void Step_AddsSignedImmediate8ToStackPointerAndUpdatesFlags(
        ushort stackPointer,
        byte offset,
        ushort expectedStackPointer,
        byte expectedFlags
    )
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xE8;
            bytes[0x0101] = offset;
        });
        cpu.Registers.SP = stackPointer;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        cpu.Registers.SP.Should().Be(expectedStackPointer);
        cpu.Registers.F.Should().Be(expectedFlags);
        cpu.Registers.PC.Should().Be(0x0102);
    }
}
