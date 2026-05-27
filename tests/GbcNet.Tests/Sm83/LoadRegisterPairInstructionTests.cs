using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class LoadRegisterPairInstructionTests
{
    [Fact]
    public void Step_LoadsStackPointerFromHlWithoutChangingFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xF9);
        cpu.Registers.HL = 0xC123;
        cpu.Registers.SP = 0xFFFE;
        cpu.Registers.F = 0xF0;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0xC123, cpu.Registers.SP);
        Assert.Equal(0xC123, cpu.Registers.HL);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }
}
