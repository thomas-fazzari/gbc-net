using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

public sealed class RegistersTests
{
    [Fact]
    public void PairProperties_ReflectEightBitRegisters()
    {
        Registers registers = new()
        {
            A = 0x12,
            F = 0x30,
            B = 0x45,
            C = 0x67,
            D = 0x89,
            E = 0xAB,
            H = 0xCD,
            L = 0xEF,
        };

        Assert.Equal(0x1230, registers.AF);
        Assert.Equal(0x4567, registers.BC);
        Assert.Equal(0x89AB, registers.DE);
        Assert.Equal(0xCDEF, registers.HL);
    }

    [Fact]
    public void PairProperties_SplitSixteenBitValues()
    {
        Registers registers = new()
        {
            AF = 0x123F,
            BC = 0x4567,
            DE = 0x89AB,
            HL = 0xCDEF,
        };

        Assert.Equal(0x12, registers.A);
        Assert.Equal(0x30, registers.F);
        Assert.Equal(0x45, registers.B);
        Assert.Equal(0x67, registers.C);
        Assert.Equal(0x89, registers.D);
        Assert.Equal(0xAB, registers.E);
        Assert.Equal(0xCD, registers.H);
        Assert.Equal(0xEF, registers.L);
    }

    [Fact]
    public void F_MasksUnusedLowerNibble()
    {
        Registers registers = new() { F = 0xFF };

        Assert.Equal(0xF0, registers.F);
    }

    [Fact]
    public void SetFlag_UpdatesOnlySelectedFlag()
    {
        Registers registers = new();

        registers.SetFlag(CpuFlag.Zero, isSet: true);
        registers.SetFlag(CpuFlag.Carry, isSet: true);
        registers.SetFlag(CpuFlag.Zero, isSet: false);

        Assert.False(registers.IsFlagSet(CpuFlag.Zero));
        Assert.False(registers.IsFlagSet(CpuFlag.Subtract));
        Assert.False(registers.IsFlagSet(CpuFlag.HalfCarry));
        Assert.True(registers.IsFlagSet(CpuFlag.Carry));
        Assert.Equal(0x10, registers.F);
    }

    [Fact]
    public void RegisterAccessors_ReadAndWriteR8Registers()
    {
        Registers registers = new()
        {
            A = 0x12,
            B = 0x34,
            C = 0x56,
            D = 0x78,
            E = 0x9A,
            H = 0xBC,
            L = 0xDE,
        };

        Assert.Equal(0x34, registers.GetRegister(Register8.B));
        Assert.Equal(0x56, registers.GetRegister(Register8.C));
        Assert.Equal(0x78, registers.GetRegister(Register8.D));
        Assert.Equal(0x9A, registers.GetRegister(Register8.E));
        Assert.Equal(0xBC, registers.GetRegister(Register8.H));
        Assert.Equal(0xDE, registers.GetRegister(Register8.L));
        Assert.Equal(0x12, registers.GetRegister(Register8.A));

        registers.SetRegister(Register8.B, 0x01);
        registers.SetRegister(Register8.C, 0x23);
        registers.SetRegister(Register8.D, 0x45);
        registers.SetRegister(Register8.E, 0x67);
        registers.SetRegister(Register8.H, 0x89);
        registers.SetRegister(Register8.L, 0xAB);
        registers.SetRegister(Register8.A, 0xCD);

        Assert.Equal(0x01, registers.B);
        Assert.Equal(0x23, registers.C);
        Assert.Equal(0x45, registers.D);
        Assert.Equal(0x67, registers.E);
        Assert.Equal(0x89, registers.H);
        Assert.Equal(0xAB, registers.L);
        Assert.Equal(0xCD, registers.A);
    }

    [Fact]
    public void RegisterPairAccessors_ReadAndWriteR16Pairs()
    {
        Registers registers = new()
        {
            BC = 0x1234,
            DE = 0x5678,
            HL = 0x9ABC,
            SP = 0xDEF0,
        };

        Assert.Equal(0x1234, registers.GetRegisterPair(RegisterPair.BC));
        Assert.Equal(0x5678, registers.GetRegisterPair(RegisterPair.DE));
        Assert.Equal(0x9ABC, registers.GetRegisterPair(RegisterPair.HL));
        Assert.Equal(0xDEF0, registers.GetRegisterPair(RegisterPair.SP));

        registers.SetRegisterPair(RegisterPair.BC, 0x1111);
        registers.SetRegisterPair(RegisterPair.DE, 0x2222);
        registers.SetRegisterPair(RegisterPair.HL, 0x3333);
        registers.SetRegisterPair(RegisterPair.SP, 0x4444);

        Assert.Equal(0x1111, registers.BC);
        Assert.Equal(0x2222, registers.DE);
        Assert.Equal(0x3333, registers.HL);
        Assert.Equal(0x4444, registers.SP);
    }
}
