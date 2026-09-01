// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

public sealed class InterruptServiceTests
{
    private const byte VBlankInterrupt = 0b0000_0001;
    private const byte LcdInterrupt = 0b0000_0010;
    private const byte TimerInterrupt = 0b0000_0100;
    private const byte SerialInterrupt = 0b0000_1000;
    private const byte JoypadInterrupt = 0b0001_0000;

    private const ushort VBlankVector = 0x0040;
    private const ushort LcdVector = 0x0048;
    private const ushort SerialVector = 0x0058;
    private const ushort OldProgramCounterStackLowByteAddress = 0xFFFC;
    private const ushort OldProgramCounterStackHighByteAddress = 0xFFFD;

    [Fact]
    public void Step_ServicesVBlankInterruptBeforeFetchingOpcode()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0x00);
        cpu.Ime = ImeState.Enabled;
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(VBlankVector);
        cpu.Registers.SP.Should().Be(OldProgramCounterStackLowByteAddress);
        bus.ReadByte(OldProgramCounterStackLowByteAddress).Should().Be(0x00);
        bus.ReadByte(OldProgramCounterStackHighByteAddress).Should().Be(0x01);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void Step_ServicesHighestPriorityRequestedAndEnabledInterrupt()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = ImeState.Enabled;
        bus.WriteByte(
            AddressMap.InterruptEnableRegister,
            VBlankInterrupt | TimerInterrupt | JoypadInterrupt
        );
        bus.WriteByte(
            AddressMap.InterruptFlagRegister,
            VBlankInterrupt | TimerInterrupt | JoypadInterrupt
        );

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(5);
        cpu.Registers.PC.Should().Be(VBlankVector);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xF4);
    }

    [Fact]
    public void Step_DoesNotServiceInterruptWhenInterruptMasterEnableIsDisabled()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0x00);
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(0x0101);
        cpu.Registers.SP.Should().Be(0xFFFE);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);
    }

    [Fact]
    public void Step_DoesNotServiceInterruptWhenNoRequestIsEnabled()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0x00);
        cpu.Ime = ImeState.Enabled;
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, TimerInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.Registers.PC.Should().Be(0x0101);
        cpu.Registers.SP.Should().Be(0xFFFE);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE4);
    }

    [Fact]
    public void Step_ServicesPendingInterruptOneStepAfterDelayedEiCompletes()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.EnablePending);
        cpu.Registers.PC.Should().Be(0x0101);

        cpu.Step().Should().Be(1);
        cpu.Ime.Should().Be(ImeState.Enabled);
        cpu.Registers.PC.Should().Be(0x0102);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);

        cpu.Step().Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(VBlankVector);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void CaptureState_PreservesDelayedInterruptEnableForContinuation()
    {
        var (source, sourceBus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });
        sourceBus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        sourceBus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        source.Step().Should().Be(1);
        var state = source.CaptureState();

        var (restored, restoredBus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });
        restoredBus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        restoredBus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);
        restored.RestoreState(state);

        restored.Step().Should().Be(source.Step());
        restored.Ime.Should().Be(ImeState.Enabled);

        restored.Step().Should().Be(source.Step());
        restored.Ime.Should().Be(ImeState.Disabled);
        restored.Registers.PC.Should().Be(VBlankVector);
        restoredBus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void Step_CancelsInterruptDispatchWhenHighBytePushDisablesAllPendingInterrupts()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = ImeState.Enabled;
        cpu.Registers.PC = 0x0200;
        cpu.Registers.SP = 0x0000;
        bus.WriteByte(AddressMap.InterruptEnableRegister, TimerInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, TimerInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(0x0000);
        cpu.Registers.SP.Should().Be(0xFFFE);
        bus.ReadByte(AddressMap.InterruptEnableRegister).Should().Be(0x02);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE4);
        bus.ReadByte(0xFFFE).Should().Be(0x00);
    }

    [Fact]
    public void Step_DispatchesRemainingInterruptWhenHighBytePushChangesEnabledMask()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = ImeState.Enabled;
        cpu.Registers.PC = 0x0200;
        cpu.Registers.SP = 0x0000;
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt | LcdInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt | LcdInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(LcdVector);
        cpu.Registers.SP.Should().Be(0xFFFE);
        bus.ReadByte(AddressMap.InterruptEnableRegister).Should().Be(0x02);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);
        bus.ReadByte(0xFFFE).Should().Be(0x00);
    }

    [Fact]
    public void Step_DoesNotCancelInterruptDispatchWhenLowBytePushDisablesSelectedInterrupt()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = ImeState.Enabled;
        cpu.Registers.PC = 0x1235;
        cpu.Registers.SP = 0x0001;
        bus.WriteByte(AddressMap.InterruptEnableRegister, SerialInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, SerialInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(SerialVector);
        cpu.Registers.SP.Should().Be(0xFFFF);
        bus.ReadByte(AddressMap.InterruptEnableRegister).Should().Be(0x35);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }

    [Fact]
    public void Step_SelectsInterruptUsingOldInterruptFlagWhenLowBytePushWritesInterruptFlag()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = ImeState.Enabled;
        cpu.Registers.PC = 0x1200;
        cpu.Registers.SP = 0xFF11;
        bus.WriteByte(AddressMap.InterruptEnableRegister, SerialInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, SerialInterrupt);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(5);
        cpu.Ime.Should().Be(ImeState.Disabled);
        cpu.Registers.PC.Should().Be(SerialVector);
        cpu.Registers.SP.Should().Be(AddressMap.InterruptFlagRegister);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE0);
    }
}
