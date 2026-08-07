// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using static GbcNet.Tests.Shared.Opcodes;

namespace GbcNet.Tests.Unit.Sm83;

// Pan Docs: STOP enters standby until a selected joypad line goes low, executing STOP resets DIV
public sealed class StopInstructionTests
{
    private const ushort EntryPoint = AddressMap.CartridgeEntryPointAddress;

    [Fact]
    public void Step_StopConsumesIgnoredSecondByteAndEntersStoppedState()
    {
        var cpu = CpuTestFactory.CreateCpu(rom =>
        {
            rom[EntryPoint] = StopOpcode;
            rom[EntryPoint + 1] = IncBOpcode;
            rom[EntryPoint + 2] = IncBOpcode;
        });

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(2);
        cpu.Stopped.Should().BeTrue();
        cpu.Registers.PC.Should().Be(EntryPoint + 2);
        cpu.Registers.B.Should().Be(0);
    }

    [Fact]
    public void Step_StoppedCpuReturnsZeroAndDoesNotFetchOrTickHardware()
    {
        var ticks = 0;
        var cpu = CpuTestFactory.CreateCpu(
            rom =>
            {
                rom[EntryPoint] = StopOpcode;
                rom[EntryPoint + 1] = NopOpcode;
                rom[EntryPoint + 2] = IncBOpcode;
            },
            () => ticks++
        );

        cpu.Step().Should().Be(2);
        var ticksAfterStopInstruction = ticks;

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(0);
        cpu.Stopped.Should().BeTrue();
        ticks.Should().Be(ticksAfterStopInstruction);
        cpu.Registers.PC.Should().Be(EntryPoint + 2);
        cpu.Registers.B.Should().Be(0);
    }

    [Fact]
    public void Step_StoppedCpuWakesWhenSelectedJoypadLineGoesLowWithoutTickingHardware()
    {
        var ticks = 0;
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(
            rom =>
            {
                rom[EntryPoint] = StopOpcode;
                rom[EntryPoint + 1] = NopOpcode;
                rom[EntryPoint + 2] = IncBOpcode;
            },
            () => ticks++
        );
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);
        cpu.Step();
        var ticksAfterStopInstruction = ticks;

        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);
        var wakeMachineCycles = cpu.Step();

        wakeMachineCycles.Should().Be(0);
        cpu.Stopped.Should().BeFalse();
        ticks.Should().Be(ticksAfterStopInstruction);

        cpu.Step().Should().Be(1);
        cpu.Registers.B.Should().Be(1);
    }

    [Fact]
    public void Step_StopResetsDividerRegister()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(rom =>
        {
            rom[EntryPoint] = StopOpcode;
            rom[EntryPoint + 1] = NopOpcode;
        });
        bus.Clock.SetCounter(0xABCC);

        cpu.Step();

        bus.ReadByte(AddressMap.DividerRegister).Should().Be(0x00);
    }

    [Fact]
    public void CaptureState_PreservesStoppedWaitAndJoypadWakeContinuation()
    {
        var (source, sourceBus) = CpuTestFactory.CreateCpuWithBus(rom =>
        {
            rom[EntryPoint] = StopOpcode;
            rom[EntryPoint + 1] = NopOpcode;
            rom[EntryPoint + 2] = IncBOpcode;
        });
        sourceBus.WriteByte(AddressMap.JoypadRegister, 0x20);

        source.Step().Should().Be(2);
        var state = source.CaptureState();

        var (restored, restoredBus) = CpuTestFactory.CreateCpuWithBus(rom =>
        {
            rom[EntryPoint] = StopOpcode;
            rom[EntryPoint + 1] = NopOpcode;
            rom[EntryPoint + 2] = IncBOpcode;
        });
        restoredBus.WriteByte(AddressMap.JoypadRegister, 0x20);
        restored.RestoreState(state);

        restored.Step().Should().Be(source.Step());
        restored.Stopped.Should().BeTrue();

        sourceBus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);
        restoredBus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);

        restored.Step().Should().Be(source.Step());
        restored.Stopped.Should().BeFalse();
        restored.Step().Should().Be(source.Step());
        restored.Registers.B.Should().Be(1);
    }
}
