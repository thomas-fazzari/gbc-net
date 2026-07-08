// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Sm83;

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

        Assert.Equal(3, machineCycles);
        Assert.Equal(0x42, bus.ReadByte(HighRamAddress));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsAccumulatorIntoHighCAddress()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0xE2);
        cpu.Registers.A = 0x34;
        cpu.Registers.C = HighRamOffset;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x34, bus.ReadByte(HighRamAddress));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
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

        Assert.Equal(3, machineCycles);
        Assert.Equal(0xA5, cpu.Registers.A);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsAccumulatorFromHighCAddress()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0xF2);
        bus.WriteByte(HighRamAddress, 0x5A);
        cpu.Registers.C = HighRamOffset;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x5A, cpu.Registers.A);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }
}
