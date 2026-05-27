using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class HaltInstructionTests
{
    private const byte HaltOpcode = 0x76;
    private const byte EnableInterruptsOpcode = 0xFB;
    private const byte IncrementBOpcode = 0x04;
    private const byte NopOpcode = 0x00;
    private const byte Restart0Opcode = 0xC7;
    private const byte VBlankInterrupt = 0b0000_0001;

    private const ushort VBlankVector = 0x0040;
    private const ushort StackReturnLowByteAddress = 0xFFFC;
    private const ushort StackReturnHighByteAddress = 0xFFFD;

    [Fact]
    public void Step_HaltsUntilAnInterruptBecomesPending()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = NopOpcode;
        });

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Halted);
        Assert.False(cpu.HaltBugPending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Halted);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_WakesHaltedCpuWithoutServicingInterruptWhenInterruptMasterEnableIsDisabled()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = NopOpcode;
        });

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Halted);

        cpu.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        cpu.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Halted);
        Assert.False(cpu.Ime);
        Assert.Equal(0x0101, cpu.Registers.PC);
        Assert.Equal(0xE1, cpu.ReadByte(AddressMap.InterruptFlagRegister));

        Assert.Equal(1, cpu.Step());
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_TriggersHaltBugWhenInterruptMasterEnableIsDisabledAndInterruptIsPending()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = IncrementBOpcode;
        });
        cpu.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        cpu.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Halted);
        Assert.True(cpu.HaltBugPending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.HaltBugPending);
        Assert.Equal(1, cpu.Registers.B);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.Equal(2, cpu.Registers.B);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_HaltBugMakesRestartReturnToRestartOpcode()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = HaltOpcode;
            bytes[0x0101] = Restart0Opcode;
        });
        cpu.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        cpu.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.HaltBugPending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(4, cpu.Step());
        Assert.False(cpu.HaltBugPending);
        Assert.Equal(0x0000, cpu.Registers.PC);
        Assert.Equal(StackReturnLowByteAddress, cpu.Registers.SP);
        Assert.Equal(0x01, cpu.ReadByte(StackReturnLowByteAddress));
        Assert.Equal(0x01, cpu.ReadByte(StackReturnHighByteAddress));
    }

    [Fact]
    public void Step_ServicesPendingInterruptAfterHaltWhenInterruptMasterEnableIsEnabled()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = HaltOpcode);
        cpu.Ime = true;

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Halted);
        Assert.False(cpu.HaltBugPending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        cpu.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        cpu.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(5, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.False(cpu.Halted);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0x01, cpu.ReadByte(StackReturnLowByteAddress));
        Assert.Equal(0x01, cpu.ReadByte(StackReturnHighByteAddress));
        Assert.Equal(0xE0, cpu.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_EiThenHaltWithPendingInterruptReturnsToHalt()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = EnableInterruptsOpcode;
            bytes[0x0101] = HaltOpcode;
        });
        cpu.WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        cpu.WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.True(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.False(cpu.Halted);
        Assert.False(cpu.HaltBugPending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(5, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0x01, cpu.ReadByte(StackReturnLowByteAddress));
        Assert.Equal(0x01, cpu.ReadByte(StackReturnHighByteAddress));
        Assert.Equal(0xE0, cpu.ReadByte(AddressMap.InterruptFlagRegister));
    }
}
