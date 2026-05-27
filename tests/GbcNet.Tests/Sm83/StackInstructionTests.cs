using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class StackInstructionTests
{
    private const byte BcStackRegisterPair = (byte)StackRegisterPair.BC;
    private const byte DeStackRegisterPair = (byte)StackRegisterPair.DE;
    private const byte HlStackRegisterPair = (byte)StackRegisterPair.HL;
    private const byte AfStackRegisterPair = (byte)StackRegisterPair.AF;

    [Theory]
    [InlineData(0xC5, BcStackRegisterPair, 0x1234, 0x1234)]
    [InlineData(0xD5, DeStackRegisterPair, 0x5678, 0x5678)]
    [InlineData(0xE5, HlStackRegisterPair, 0x9ABC, 0x9ABC)]
    [InlineData(0xF5, AfStackRegisterPair, 0xDEF3, 0xDEF0)]
    public void Step_PushesRegisterPairOntoStack(
        byte opcode,
        byte registerPair,
        ushort value,
        ushort expectedValue
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.Registers.SetStackRegisterPair((StackRegisterPair)registerPair, value);

        int machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(0xC0FE, cpu.Registers.SP);
        Assert.Equal((byte)expectedValue, cpu.ReadByte(0xC0FE));
        Assert.Equal((byte)(expectedValue >> 8), cpu.ReadByte(0xC0FF));
        Assert.Equal(
            expectedValue,
            cpu.Registers.GetStackRegisterPair((StackRegisterPair)registerPair)
        );
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0xC1, BcStackRegisterPair, 0x1234, 0x1234)]
    [InlineData(0xD1, DeStackRegisterPair, 0x5678, 0x5678)]
    [InlineData(0xE1, HlStackRegisterPair, 0x9ABC, 0x9ABC)]
    [InlineData(0xF1, AfStackRegisterPair, 0xDEF3, 0xDEF0)]
    public void Step_PopsRegisterPairFromStack(
        byte opcode,
        byte registerPair,
        ushort stackValue,
        ushort expectedValue
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.SP = 0xC100;
        cpu.WriteByte(0xC100, (byte)stackValue);
        cpu.WriteByte(0xC101, (byte)(stackValue >> 8));

        int machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(0xC102, cpu.Registers.SP);
        Assert.Equal(
            expectedValue,
            cpu.Registers.GetStackRegisterPair((StackRegisterPair)registerPair)
        );
        Assert.Equal(0x0101, cpu.Registers.PC);
    }
}
