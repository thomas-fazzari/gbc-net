namespace GbcNet.Tests.Sm83;

public sealed class InterruptControlInstructionTests
{
    [Fact]
    public void Step_DisablesInterruptMasterEnableImmediately()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xF3);
        cpu.Ime = true;

        var machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.False(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_EnablesInterruptMasterEnableAfterFollowingInstruction()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xFB;
            bytes[0x0101] = 0x00;
        });

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.True(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.True(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0102, cpu.Registers.PC);
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

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.True(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0102, cpu.Registers.PC);

        Assert.Equal(1, cpu.Step());
        Assert.False(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0103, cpu.Registers.PC);
    }

    [Fact]
    public void Step_EnableInterruptMasterEnableWhenAlreadyEnabledDoesNotDelay()
    {
        var cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xFB);
        cpu.Ime = true;

        var machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.True(cpu.Ime);
        Assert.False(cpu.ImeEnablePending);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }
}
