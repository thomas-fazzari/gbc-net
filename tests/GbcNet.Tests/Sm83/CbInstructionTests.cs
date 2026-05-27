using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class CbInstructionTests
{
    private const byte ARegister = (byte)Register8.A;
    private const byte BRegister = (byte)Register8.B;
    private const byte HRegister = (byte)Register8.H;

    private const byte HalfCarryAndCarryFlags = (byte)CpuFlag.HalfCarry | (byte)CpuFlag.Carry;

    private const byte ZeroHalfCarryAndCarryFlags =
        (byte)CpuFlag.Zero | (byte)CpuFlag.HalfCarry | (byte)CpuFlag.Carry;

    [Theory]
    [InlineData(0x40, BRegister, 0x01, HalfCarryAndCarryFlags)]
    [InlineData(0x40, BRegister, 0x00, ZeroHalfCarryAndCarryFlags)]
    [InlineData(0x7C, HRegister, 0x80, HalfCarryAndCarryFlags)]
    [InlineData(0x7F, ARegister, 0x00, ZeroHalfCarryAndCarryFlags)]
    public void Step_ExecutesPrefixedBitRegisterOperand(
        byte prefixedOpcode,
        byte register,
        byte value,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = prefixedOpcode;
        });
        var register8 = (Register8)register;
        cpu.Registers.SetRegister(register8, value);
        cpu.Registers.F = (byte)CpuFlag.Carry;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(value, cpu.Registers.GetRegister(register8));
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x46, 0x01, HalfCarryAndCarryFlags)]
    [InlineData(0x46, 0x00, ZeroHalfCarryAndCarryFlags)]
    [InlineData(0x7E, 0x80, HalfCarryAndCarryFlags)]
    public void Step_ExecutesPrefixedBitAddressHlOperand(
        byte prefixedOpcode,
        byte value,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = prefixedOpcode;
        });
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = (byte)CpuFlag.Carry;
        cpu.WriteByte(0xC123, value);

        int machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(value, cpu.ReadByte(0xC123));
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_ExecutesEveryPrefixedBitOpcode()
    {
        for (byte prefixedOpcode = 0x40; prefixedOpcode <= 0x7F; prefixedOpcode++)
        {
            Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
            {
                bytes[0x0100] = 0xCB;
                bytes[0x0101] = prefixedOpcode;
            });
            cpu.Registers.A = 0xFF;
            cpu.Registers.B = 0xFF;
            cpu.Registers.C = 0xFF;
            cpu.Registers.D = 0xFF;
            cpu.Registers.E = 0xFF;
            cpu.Registers.H = 0xC1;
            cpu.Registers.L = 0x23;
            cpu.WriteByte(0xC123, 0xFF);

            int machineCycles = cpu.Step();

            Assert.Equal((prefixedOpcode & 0x07) == 0x06 ? 3 : 2, machineCycles);
            Assert.Equal(0x0102, cpu.Registers.PC);
        }
    }

    [Fact]
    public void Step_RejectsUnsupportedPrefixedOpcode()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = 0x00;
        });

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => cpu.Step());

        Assert.Equal("CB opcode 0x00 is not supported yet.", exception.Message);
    }
}
