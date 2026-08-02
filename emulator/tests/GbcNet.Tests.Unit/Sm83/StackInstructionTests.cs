// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

public sealed class StackInstructionTests
{
    private const byte BcStackRegisterPair = (byte)StackRegisterPair.BC;
    private const byte DeStackRegisterPair = (byte)StackRegisterPair.DE;
    private const byte HlStackRegisterPair = (byte)StackRegisterPair.HL;
    private const byte AfStackRegisterPair = (byte)StackRegisterPair.AF;

    [Theory]
    [InlineData(0xC5, BcStackRegisterPair, 0x1234, 0x1234)]
    [InlineData(0xD5, DeStackRegisterPair, 0x5678, 0x5678)]
    [InlineData(0xE5, HlStackRegisterPair, 0x9ABC, 0x9ABC)]
    [InlineData(0xF5, AfStackRegisterPair, 0xDEF3, 0xDEF0)]
    public void Step_PushesRegisterPairOntoStack(
        byte opcode,
        byte registerPair,
        ushort value,
        ushort expectedValue
    )
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.SetStackRegisterPair((StackRegisterPair)registerPair, value);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        cpu.Registers.SP.Should().Be(0xC0FE);
        bus.ReadByte(0xC0FE).Should().Be((byte)expectedValue);
        bus.ReadByte(0xC0FF).Should().Be((byte)(expectedValue >> 8));
        cpu.Registers.GetStackRegisterPair((StackRegisterPair)registerPair)
            .Should()
            .Be(expectedValue);
        cpu.Registers.PC.Should().Be(0x0101);
    }

    [Theory]
    [InlineData(0xC1, BcStackRegisterPair, 0x1234, 0x1234)]
    [InlineData(0xD1, DeStackRegisterPair, 0x5678, 0x5678)]
    [InlineData(0xE1, HlStackRegisterPair, 0x9ABC, 0x9ABC)]
    [InlineData(0xF1, AfStackRegisterPair, 0xDEF3, 0xDEF0)]
    public void Step_PopsRegisterPairFromStack(
        byte opcode,
        byte registerPair,
        ushort stackValue,
        ushort expectedValue
    )
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        bus.WriteByte(0xC100, (byte)stackValue);
        bus.WriteByte(0xC101, (byte)(stackValue >> 8));

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(3);
        cpu.Registers.SP.Should().Be(0xC102);
        cpu.Registers.GetStackRegisterPair((StackRegisterPair)registerPair)
            .Should()
            .Be(expectedValue);
        cpu.Registers.PC.Should().Be(0x0101);
    }
}
