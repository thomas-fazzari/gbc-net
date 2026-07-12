// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Sm83;

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
        cpu.Ime = true;
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(OldProgramCounterStackLowByteAddress, cpu.Registers.SP);
        Assert.Equal(0x00, bus.ReadByte(OldProgramCounterStackLowByteAddress));
        Assert.Equal(0x01, bus.ReadByte(OldProgramCounterStackHighByteAddress));
        Assert.Equal(0xE0, bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_ServicesHighestPriorityRequestedAndEnabledInterrupt()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = true;
        bus.WriteByte(
            AddressMap.InterruptEnableRegister,
            VBlankInterrupt | TimerInterrupt | JoypadInterrupt
        );
        bus.WriteByte(
            AddressMap.InterruptFlagRegister,
            VBlankInterrupt | TimerInterrupt | JoypadInterrupt
        );

        var machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0xF4, bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_DoesNotServiceInterruptWhenInterruptMasterEnableIsDisabled()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0x00);
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(0x0101, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
        Assert.Equal(0xE1, bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_DoesNotServiceInterruptWhenNoRequestIsEnabled()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus(bytes => bytes[0x0100] = 0x00);
        cpu.Ime = true;
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, TimerInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.True(cpu.Ime);
        Assert.Equal(0x0101, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
        Assert.Equal(0xE4, bus.ReadByte(AddressMap.InterruptFlagRegister));
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

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.True(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0102, cpu.Registers.PC);
        Assert.Equal(0xE1, bus.ReadByte(AddressMap.InterruptFlagRegister));

        Assert.Equal(5, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0xE0, bus.ReadByte(AddressMap.InterruptFlagRegister));
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

        Assert.Equal(1, source.Step());
        var state = source.CaptureState();

        var (restored, restoredBus) = CpuTestFactory.CreateCpuWithBus(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });
        restoredBus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        restoredBus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);
        restored.RestoreState(state);

        Assert.Equal(source.Step(), restored.Step());
        Assert.True(restored.Ime);
        Assert.False(restored.ImeEnablePending);

        Assert.Equal(source.Step(), restored.Step());
        Assert.False(restored.Ime);
        Assert.Equal(VBlankVector, restored.Registers.PC);
        Assert.Equal(0xE0, restoredBus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_CancelsInterruptDispatchWhenHighBytePushDisablesAllPendingInterrupts()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = true;
        cpu.Registers.PC = 0x0200;
        cpu.Registers.SP = 0x0000;
        bus.WriteByte(AddressMap.InterruptEnableRegister, TimerInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, TimerInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(0x0000, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
        Assert.Equal(0x02, bus.ReadByte(AddressMap.InterruptEnableRegister));
        Assert.Equal(0xE4, bus.ReadByte(AddressMap.InterruptFlagRegister));
        Assert.Equal(0x00, bus.ReadByte(0xFFFE));
    }

    [Fact]
    public void Step_DispatchesRemainingInterruptWhenHighBytePushChangesEnabledMask()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = true;
        cpu.Registers.PC = 0x0200;
        cpu.Registers.SP = 0x0000;
        bus.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt | LcdInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt | LcdInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(LcdVector, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
        Assert.Equal(0x02, bus.ReadByte(AddressMap.InterruptEnableRegister));
        Assert.Equal(0xE1, bus.ReadByte(AddressMap.InterruptFlagRegister));
        Assert.Equal(0x00, bus.ReadByte(0xFFFE));
    }

    [Fact]
    public void Step_DoesNotCancelInterruptDispatchWhenLowBytePushDisablesSelectedInterrupt()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = true;
        cpu.Registers.PC = 0x1235;
        cpu.Registers.SP = 0x0001;
        bus.WriteByte(AddressMap.InterruptEnableRegister, SerialInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, SerialInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(SerialVector, cpu.Registers.PC);
        Assert.Equal(0xFFFF, cpu.Registers.SP);
        Assert.Equal(0x35, bus.ReadByte(AddressMap.InterruptEnableRegister));
        Assert.Equal(0xE0, bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_SelectsInterruptUsingOldInterruptFlagWhenLowBytePushWritesInterruptFlag()
    {
        var (cpu, bus) = CpuTestFactory.CreateCpuWithBus();
        cpu.Ime = true;
        cpu.Registers.PC = 0x1200;
        cpu.Registers.SP = 0xFF11;
        bus.WriteByte(AddressMap.InterruptEnableRegister, SerialInterrupt);
        bus.WriteByte(AddressMap.InterruptFlagRegister, SerialInterrupt);

        var machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(SerialVector, cpu.Registers.PC);
        Assert.Equal(AddressMap.InterruptFlagRegister, cpu.Registers.SP);
        Assert.Equal(0xE0, bus.ReadByte(AddressMap.InterruptFlagRegister));
    }
}
