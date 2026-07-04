// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Sm83;

public sealed class LoadAddressInstructionTests
{
    private const ushort WorkRamAddress = AddressMap.WorkRamStart + 0x0123;
    private const byte WorkRamAddressLowByte = 0x23;
    private const byte WorkRamAddressHighByte = 0xC1;

    [Fact]
    public void Step_LoadsAccumulatorIntoImmediate16Address()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xEA;
            bytes[0x0101] = WorkRamAddressLowByte;
            bytes[0x0102] = WorkRamAddressHighByte;
        });
        cpu.Registers.A = 0x42;
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(0x42, CpuTestFactory.GetBus(cpu).ReadByte(WorkRamAddress));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0103, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsAccumulatorFromImmediate16Address()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xFA;
            bytes[0x0101] = WorkRamAddressLowByte;
            bytes[0x0102] = WorkRamAddressHighByte;
        });
        CpuTestFactory.GetBus(cpu).WriteByte(WorkRamAddress, 0xA5);
        cpu.Registers.F = 0xF0;

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(0xA5, cpu.Registers.A);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0103, cpu.Registers.PC);
    }
}
