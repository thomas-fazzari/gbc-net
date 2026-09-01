// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Sm83;

public sealed class LoadAddressInstructionTests
{
    private const ushort WorkRamAddress = AddressMap.WorkRamStart + 0x0123;
    private const byte WorkRamAddressLowByte = 0x23;
    private const byte WorkRamAddressHighByte = 0xC1;

    [Fact]
    public void Step_LoadsAccumulatorIntoImmediate16Address()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xEA;
            bytes[0x0101] = WorkRamAddressLowByte;
            bytes[0x0102] = WorkRamAddressHighByte;
        });
        cpu.Registers.A = 0x42;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        bus.ReadByte(WorkRamAddress).Should().Be(0x42);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0103);
    }

    [Fact]
    public void Step_LoadsAccumulatorFromImmediate16Address()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xFA;
            bytes[0x0101] = WorkRamAddressLowByte;
            bytes[0x0102] = WorkRamAddressHighByte;
        });
        bus.WriteByte(WorkRamAddress, 0xA5);
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(4);
        cpu.Registers.A.Should().Be(0xA5);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0103);
    }
}
