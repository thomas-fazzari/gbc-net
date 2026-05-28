using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class InterruptServiceTests
{
    private const byte VBlankInterrupt = 0b0000_0001;
    private const byte TimerInterrupt = 0b0000_0100;
    private const byte JoypadInterrupt = 0b0001_0000;

    private const ushort VBlankVector = 0x0040;
    private const ushort OldProgramCounterStackLowByteAddress = 0xFFFC;
    private const ushort OldProgramCounterStackHighByteAddress = 0xFFFD;

    [Fact]
    public void Step_ServicesVBlankInterruptBeforeFetchingOpcode()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x00);
        cpu.Ime = true;
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        int machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(OldProgramCounterStackLowByteAddress, cpu.Registers.SP);
        Assert.Equal(
            0x00,
            CpuTestFactory.GetBus(cpu).ReadByte(OldProgramCounterStackLowByteAddress)
        );
        Assert.Equal(
            0x01,
            CpuTestFactory.GetBus(cpu).ReadByte(OldProgramCounterStackHighByteAddress)
        );
        Assert.Equal(0xE0, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_ServicesHighestPriorityRequestedAndEnabledInterrupt()
    {
        Cpu cpu = CpuTestFactory.CreateCpu();
        cpu.Ime = true;
        CpuTestFactory
            .GetBus(cpu)
            .WriteByte(
                AddressMap.InterruptEnableRegister,
                VBlankInterrupt | TimerInterrupt | JoypadInterrupt
            );
        CpuTestFactory
            .GetBus(cpu)
            .WriteByte(
                AddressMap.InterruptFlagRegister,
                VBlankInterrupt | TimerInterrupt | JoypadInterrupt
            );

        int machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0xF4, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_DoesNotServiceInterruptWhenInterruptMasterEnableIsDisabled()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x00);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.False(cpu.Ime);
        Assert.Equal(0x0101, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
        Assert.Equal(0xE1, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_DoesNotServiceInterruptWhenNoRequestIsEnabled()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x00);
        cpu.Ime = true;
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptFlagRegister, TimerInterrupt);

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.True(cpu.Ime);
        Assert.Equal(0x0101, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
        Assert.Equal(0xE4, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Step_ServicesPendingInterruptOneStepAfterDelayedEiCompletes()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptEnableRegister, VBlankInterrupt);
        CpuTestFactory.GetBus(cpu).WriteByte(AddressMap.InterruptFlagRegister, VBlankInterrupt);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.True(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0102, cpu.Registers.PC);
        Assert.Equal(0xE1, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));

        Assert.Equal(5, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.Equal(VBlankVector, cpu.Registers.PC);
        Assert.Equal(0xE0, CpuTestFactory.GetBus(cpu).ReadByte(AddressMap.InterruptFlagRegister));
    }
}
