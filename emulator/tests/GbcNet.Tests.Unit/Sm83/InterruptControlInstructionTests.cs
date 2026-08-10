// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

// Pan Docs `interrupts.md`: EI is delayed by one instruction, while DI takes effect immediately.
public sealed class InterruptControlInstructionTests
{
    [Fact]
    public void Step_DisablesInterruptMasterEnableImmediately()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xF3);
        cpu.Ime = ImeState.Enabled;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(0x0101);
    }

    [Fact]
    public void Step_EnablesInterruptMasterEnableAfterFollowingInstruction()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.EnablePending);
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.Registers.PC.Should().Be(0x0102);
    }

    [Fact]
    public void Step_EnableThenDisableInterruptMasterEnableKeepsInterruptsDisabled()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0xF3;
            bytes[0x0102] = 0x00;
        });

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.EnablePending);
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(0x0102);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(0x0103);
    }

    [Fact]
    public void Step_EnableInterruptMasterEnableWhenAlreadyEnabledDoesNotDelay()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xFB);
        cpu.Ime = ImeState.Enabled;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.Registers.PC.Should().Be(0x0101);
    }
}
