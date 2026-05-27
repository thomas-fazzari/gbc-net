using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class CbSetResetInstructionTests
{
    private const byte ARegister = (byte)Register8.A;
    private const byte BRegister = (byte)Register8.B;

    private const byte AllFlags = 0xF0;

    [Theory]
    [InlineData(0x80, BRegister, 0xFF, 0xFE)]
    [InlineData(0x87, ARegister, 0x01, 0x00)]
    [InlineData(0xBF, ARegister, 0x80, 0x00)]
    [InlineData(0xC0, BRegister, 0x00, 0x01)]
    [InlineData(0xC7, ARegister, 0x00, 0x01)]
    [InlineData(0xFF, ARegister, 0x00, 0x80)]
    public void Step_ExecutesPrefixedSetResetRegisterOperand(
        byte prefixedOpcode,
        byte register,
        byte value,
        byte expectedValue
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = prefixedOpcode;
        });
        var register8 = (Register8)register;
        cpu.Registers.SetRegister(register8, value);
        cpu.Registers.F = AllFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(expectedValue, cpu.Registers.GetRegister(register8));
        Assert.Equal(AllFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x86, 0xFF, 0xFE)]
    [InlineData(0xBE, 0x80, 0x00)]
    [InlineData(0xC6, 0x00, 0x01)]
    [InlineData(0xFE, 0x00, 0x80)]
    public void Step_ExecutesPrefixedSetResetAddressHlOperand(
        byte prefixedOpcode,
        byte value,
        byte expectedValue
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = prefixedOpcode;
        });
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = AllFlags;
        cpu.WriteByte(0xC123, value);

        int machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(expectedValue, cpu.ReadByte(0xC123));
        Assert.Equal(AllFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_ExecutesEveryPrefixedSetResetOpcode()
    {
        for (int prefixedOpcode = 0x80; prefixedOpcode <= 0xFF; prefixedOpcode++)
        {
            Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
            {
                bytes[0x0100] = 0xCB;
                bytes[0x0101] = (byte)prefixedOpcode;
            });
            cpu.Registers.A = 0xFF;
            cpu.Registers.B = 0xFF;
            cpu.Registers.C = 0xFF;
            cpu.Registers.D = 0xFF;
            cpu.Registers.E = 0xFF;
            cpu.Registers.H = 0xC1;
            cpu.Registers.L = 0x23;
            cpu.Registers.F = AllFlags;
            cpu.WriteByte(0xC123, 0xFF);

            int machineCycles = cpu.Step();

            Assert.Equal((prefixedOpcode & 0x07) == 0x06 ? 4 : 2, machineCycles);
            Assert.Equal(AllFlags, cpu.Registers.F);
            Assert.Equal(0x0102, cpu.Registers.PC);
        }
    }
}
