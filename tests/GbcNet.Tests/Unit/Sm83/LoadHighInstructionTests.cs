// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Sm83;

public sealed class LoadHighInstructionTests
{
    // Use HRAM so this test validates LDH addressing without asserting temporary I/O behavior
    private const byte HighRamOffset = 0x80;
    private const ushort HighRamAddress = AddressMap.HighRamStart;

    [Fact]
    public void Step_LoadsAccumulatorIntoHighImmediate8Address()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xE0;
            bytes[0x0101] = HighRamOffset;
        });
        cpu.Registers.A = 0x42;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(3);
        bus.ReadByte(HighRamAddress).Should().Be(0x42);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0102);
    }

    [Fact]
    public void Step_LoadsAccumulatorIntoHighCAddress()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0xE2);
        cpu.Registers.A = 0x34;
        cpu.Registers.C = HighRamOffset;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(2);
        bus.ReadByte(HighRamAddress).Should().Be(0x34);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0101);
    }

    [Fact]
    public void Step_LoadsAccumulatorFromHighImmediate8Address()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xF0;
            bytes[0x0101] = HighRamOffset;
        });
        bus.WriteByte(HighRamAddress, 0xA5);
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(3);
        cpu.Registers.A.Should().Be(0xA5);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0102);
    }

    [Fact]
    public void Step_LoadsAccumulatorFromHighCAddress()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0xF2);
        bus.WriteByte(HighRamAddress, 0x5A);
        cpu.Registers.C = HighRamOffset;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(2);
        cpu.Registers.A.Should().Be(0x5A);
        cpu.Registers.F.Should().Be(0xF0);
        cpu.Registers.PC.Should().Be(0x0101);
    }
}
