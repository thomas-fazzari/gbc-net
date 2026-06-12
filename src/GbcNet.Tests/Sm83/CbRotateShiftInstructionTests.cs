using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class CbRotateShiftInstructionTests
{
    private const byte BRegister = (byte)Register8.B;

    private const byte NoFlags = 0x00;
    private const byte CarryFlag = (byte)CpuFlag.Carry;
    private const byte ZeroFlag = (byte)CpuFlag.Zero;
    private const byte ZeroAndCarryFlags = (byte)CpuFlag.Zero | (byte)CpuFlag.Carry;
    private const byte AllFlags = 0xF0;

    [Theory]
    [InlineData(0x00, BRegister, 0x80, NoFlags, 0x01, CarryFlag)]
    [InlineData(0x00, BRegister, 0x00, AllFlags, 0x00, ZeroFlag)]
    [InlineData(0x08, BRegister, 0x01, NoFlags, 0x80, CarryFlag)]
    [InlineData(0x08, BRegister, 0x00, AllFlags, 0x00, ZeroFlag)]
    [InlineData(0x10, BRegister, 0x80, CarryFlag, 0x01, CarryFlag)]
    [InlineData(0x10, BRegister, 0x00, CarryFlag, 0x01, NoFlags)]
    [InlineData(0x10, BRegister, 0x80, NoFlags, 0x00, ZeroAndCarryFlags)]
    [InlineData(0x18, BRegister, 0x01, CarryFlag, 0x80, CarryFlag)]
    [InlineData(0x18, BRegister, 0x00, CarryFlag, 0x80, NoFlags)]
    [InlineData(0x18, BRegister, 0x01, NoFlags, 0x00, ZeroAndCarryFlags)]
    [InlineData(0x20, BRegister, 0x80, AllFlags, 0x00, ZeroAndCarryFlags)]
    [InlineData(0x20, BRegister, 0x01, AllFlags, 0x02, NoFlags)]
    [InlineData(0x28, BRegister, 0x81, AllFlags, 0xC0, CarryFlag)]
    [InlineData(0x28, BRegister, 0x01, AllFlags, 0x00, ZeroAndCarryFlags)]
    [InlineData(0x30, BRegister, 0xF0, AllFlags, 0x0F, NoFlags)]
    [InlineData(0x30, BRegister, 0x00, AllFlags, 0x00, ZeroFlag)]
    [InlineData(0x38, BRegister, 0x01, AllFlags, 0x00, ZeroAndCarryFlags)]
    [InlineData(0x38, BRegister, 0x80, AllFlags, 0x40, NoFlags)]
    public void Step_ExecutesPrefixedRotateShiftRegisterOperand(
        byte prefixedOpcode,
        byte register,
        byte value,
        byte initialFlags,
        byte expectedValue,
        byte expectedFlags
    )
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = prefixedOpcode;
        });
        var register8 = (Register8)register;
        cpu.Registers.SetRegister(register8, value);
        cpu.Registers.F = initialFlags;

        var machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(expectedValue, cpu.Registers.GetRegister(register8));
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x06, 0x80, AllFlags, 0x01, CarryFlag)]
    [InlineData(0x16, 0x00, CarryFlag, 0x01, NoFlags)]
    [InlineData(0x2E, 0x81, AllFlags, 0xC0, CarryFlag)]
    [InlineData(0x36, 0xF0, AllFlags, 0x0F, NoFlags)]
    [InlineData(0x3E, 0x01, AllFlags, 0x00, ZeroAndCarryFlags)]
    public void Step_ExecutesPrefixedRotateShiftAddressHlOperand(
        byte prefixedOpcode,
        byte value,
        byte initialFlags,
        byte expectedValue,
        byte expectedFlags
    )
    {
        var cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0xCB;
            bytes[0x0101] = prefixedOpcode;
        });
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = initialFlags;
        CpuTestFactory.GetBus(cpu).WriteByte(0xC123, value);

        var machineCycles = cpu.Step();

        Assert.Equal(4, machineCycles);
        Assert.Equal(expectedValue, CpuTestFactory.GetBus(cpu).ReadByte(0xC123));
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_ExecutesEveryPrefixedRotateShiftOpcode()
    {
        for (byte prefixedOpcode = 0x00; prefixedOpcode <= 0x3F; prefixedOpcode++)
        {
            var opcode = prefixedOpcode;
            var cpu = CpuTestFactory.CreateCpu(bytes =>
            {
                bytes[0x0100] = 0xCB;
                bytes[0x0101] = opcode;
            });
            cpu.Registers.A = 0x81;
            cpu.Registers.B = 0x81;
            cpu.Registers.C = 0x81;
            cpu.Registers.D = 0x81;
            cpu.Registers.E = 0x81;
            cpu.Registers.H = 0xC1;
            cpu.Registers.L = 0x23;
            CpuTestFactory.GetBus(cpu).WriteByte(0xC123, 0x81);

            var machineCycles = cpu.Step();

            Assert.Equal((prefixedOpcode & 0x07) == 0x06 ? 4 : 2, machineCycles);
            Assert.Equal(0x0102, cpu.Registers.PC);
        }
    }
}
