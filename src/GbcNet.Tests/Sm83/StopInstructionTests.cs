using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

// Pan Docs: STOP enters standby until a selected joypad line goes low, executing STOP resets DIV
public sealed class StopInstructionTests
{
    private const ushort EntryPoint = AddressMap.CartridgeEntryPointStart;
    private const byte StopOpcode = 0x10;
    private const byte NopOpcode = 0x00;
    private const byte IncBOpcode = 0x04;

    [Fact]
    public void Step_StopConsumesIgnoredSecondByteAndEntersStoppedState()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(rom =>
        {
            rom[EntryPoint] = StopOpcode;
            rom[EntryPoint + 1] = IncBOpcode;
            rom[EntryPoint + 2] = IncBOpcode;
        });

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.True(cpu.Stopped);
        Assert.Equal(EntryPoint + 2, cpu.Registers.PC);
        Assert.Equal(0, cpu.Registers.B);
    }

    [Fact]
    public void Step_StoppedCpuReturnsZeroAndDoesNotFetchOrTickHardware()
    {
        int ticks = 0;
        Cpu cpu = CpuTestFactory.CreateCpu(
            rom =>
            {
                rom[EntryPoint] = StopOpcode;
                rom[EntryPoint + 1] = NopOpcode;
                rom[EntryPoint + 2] = IncBOpcode;
            },
            () => ticks++
        );

        Assert.Equal(2, cpu.Step());
        int ticksAfterStopInstruction = ticks;

        int machineCycles = cpu.Step();

        Assert.Equal(0, machineCycles);
        Assert.True(cpu.Stopped);
        Assert.Equal(ticksAfterStopInstruction, ticks);
        Assert.Equal(EntryPoint + 2, cpu.Registers.PC);
        Assert.Equal(0, cpu.Registers.B);
    }

    [Fact]
    public void Step_StoppedCpuWakesWhenSelectedJoypadLineGoesLowWithoutTickingHardware()
    {
        int ticks = 0;
        Cpu cpu = CpuTestFactory.CreateCpu(
            rom =>
            {
                rom[EntryPoint] = StopOpcode;
                rom[EntryPoint + 1] = NopOpcode;
                rom[EntryPoint + 2] = IncBOpcode;
            },
            () => ticks++
        );
        MemoryBus bus = CpuTestFactory.GetBus(cpu);
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);
        cpu.Step();
        int ticksAfterStopInstruction = ticks;

        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);
        int wakeMachineCycles = cpu.Step();

        Assert.Equal(0, wakeMachineCycles);
        Assert.False(cpu.Stopped);
        Assert.Equal(ticksAfterStopInstruction, ticks);

        Assert.Equal(1, cpu.Step());
        Assert.Equal(1, cpu.Registers.B);
    }

    [Fact]
    public void Step_StopResetsDividerRegister()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(rom =>
        {
            rom[EntryPoint] = StopOpcode;
            rom[EntryPoint + 1] = NopOpcode;
        });
        MemoryBus bus = CpuTestFactory.GetBus(cpu);
        bus.Clock.SetCounter(0xABCC);

        cpu.Step();

        Assert.Equal(0x00, bus.ReadByte(AddressMap.DividerRegister));
    }
}
