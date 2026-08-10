// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;
using static GbcNet.Tests.Shared.Opcodes;

namespace GbcNet.Tests.Unit.Sm83;

// Pan Docs `halt.md`: HALT wakes for a pending enabled interrupt, while the HALT bug changes one fetch.
public sealed class HaltInstructionTests
{
    private const byte EnableInterruptsOpcode = 0xFB;
    private const byte Restart0Opcode = 0xC7;
    private const byte VBlankInterrupt = 0b0000_0001;

    private const ushort VBlankVector = 0x0040;
    private const ushort StackReturnLowByteAddress = 0xFFFC;
    private const ushort StackReturnHighByteAddress = 0xFFFD;

    [Fact]
    public void Step_HaltsUntilAnInterruptBecomesPending()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = NopOpcode;
        });

        cpu.Step().Should().Be(1);
        cpu.RunState.Should().Be(CpuRunState.Halted);
        cpu.HaltBugPending.Should().BeFalse();
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.RunState.Should().Be(CpuRunState.Halted);
        cpu.Registers.PC.Should().Be(0x0101);
    }

    [Fact]
    public void Step_WakesHaltedCpuWithoutServicingInterruptWhenInterruptMasterEnableIsDisabled()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = NopOpcode;
        });

        cpu.Step().Should().Be(1);
        cpu.RunState.Should().Be(CpuRunState.Halted);

        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(1);
        cpu.RunState.Should().Be(CpuRunState.Running);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(0x0101);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);

        cpu.Step().Should().Be(1);
        cpu.Registers.PC.Should().Be(0x0102);
    }

    [Fact]
    public void Step_TriggersHaltBugWhenInterruptMasterEnableIsDisabledAndInterruptIsPending()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = IncBOpcode;
        });
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(1);
        cpu.RunState.Should().Be(CpuRunState.Running);
        cpu.HaltBugPending.Should().BeTrue();
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.HaltBugPending.Should().BeFalse();
        cpu.Registers.B.Should().Be(1);
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.Registers.B.Should().Be(2);
        cpu.Registers.PC.Should().Be(0x0102);
    }

    [Fact]
    public void Step_HaltBugMakesRestartReturnToRestartOpcode()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = Restart0Opcode;
        });
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(1);
        cpu.HaltBugPending.Should().BeTrue();
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(4);
        cpu.HaltBugPending.Should().BeFalse();
        cpu.Registers.PC.Should().Be(0x0000);
        cpu.Registers.SP.Should().Be(StackReturnLowByteAddress);
        bus.ReadByte(StackReturnLowByteAddress).Should().Be(0x01);
        bus.ReadByte(StackReturnHighByteAddress).Should().Be(0x01);
    }

    [Fact]
    public void Step_ServicesPendingInterruptAfterHaltWhenInterruptMasterEnableIsEnabled()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = HaltOpcode);
        cpu.Ime = ImeState.Enabled;

        cpu.Step().Should().Be(1);
        cpu.RunState.Should().Be(CpuRunState.Halted);
        cpu.HaltBugPending.Should().BeFalse();
        cpu.Registers.PC.Should().Be(0x0101);

        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(6);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.RunState.Should().Be(CpuRunState.Running);
        cpu.Registers.PC.Should().Be(VBlankVector);
        bus.ReadByte(StackReturnLowByteAddress).Should().Be(0x01);
        bus.ReadByte(StackReturnHighByteAddress).Should().Be(0x01);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void Step_EiThenHaltWithPendingInterruptReturnsToHalt()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = EnableInterruptsOpcode;
            bytes[0x0101] = HaltOpcode;
        });
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.EnablePending);
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.RunState.Should().Be(CpuRunState.Running);
        cpu.HaltBugPending.Should().BeFalse();
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(VBlankVector);
        bus.ReadByte(StackReturnLowByteAddress).Should().Be(0x01);
        bus.ReadByte(StackReturnHighByteAddress).Should().Be(0x01);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void CaptureState_ResumesHaltedCpuByServicingPendingInterrupt()
    {
        var (source, sourceBus) = CpuTestFactory.CreateCpuWithBus(bytes =>
            bytes[0x0100] = HaltOpcode
        );
        source.Ime = ImeState.Enabled;

        source.Step().Should().Be(1);
        var state = source.CaptureState();

        var (restored, restoredBus) = CpuTestFactory.CreateCpuWithBus(bytes =>
            bytes[0x0100] = HaltOpcode
        );
        restored.RestoreState(state);
        sourceBus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        sourceBus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);
        restoredBus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        restoredBus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        restored.Step().Should().Be(source.Step());
        restored.Registers.PC.Should().Be(VBlankVector);
        restored.Registers.SP.Should().Be(StackReturnLowByteAddress);
        restoredBus.ReadByte(StackReturnLowByteAddress).Should().Be(0x01);
        restoredBus.ReadByte(StackReturnHighByteAddress).Should().Be(0x01);
        restoredBus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void CaptureState_PreservesPendingHaltBugForNextInstruction()
    {
        var (source, sourceBus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = IncBOpcode;
        });
        sourceBus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        sourceBus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        source.Step().Should().Be(1);
        var state = source.CaptureState();

        var restored = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = IncBOpcode;
        });
        restored.RestoreState(state);

        restored.Step().Should().Be(source.Step());
        restored.Registers.B.Should().Be(1);
        restored.Registers.PC.Should().Be(0x0101);
    }
}
