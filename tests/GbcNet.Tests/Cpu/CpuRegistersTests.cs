using GbcNet.Core.Cpu;

namespace GbcNet.Tests.Cpu;

public sealed class CpuRegistersTests
{
    [Fact]
    public void PairProperties_ReflectEightBitRegisters()
    {
        CpuRegisters registers = new()
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
        CpuRegisters registers = new()
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
        CpuRegisters registers = new() { F = 0xFF };

        Assert.Equal(0xF0, registers.F);
    }

    [Fact]
    public void SetFlag_UpdatesOnlySelectedFlag()
    {
        CpuRegisters registers = new();

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
    public void RegisterPairAccessors_ReadAndWriteR16Pairs()
    {
        CpuRegisters registers = new()
        {
            BC = 0x1234,
            DE = 0x5678,
            HL = 0x9ABC,
            SP = 0xDEF0,
        };

        Assert.Equal(0x1234, registers.GetRegisterPair(Sm83RegisterPair.BC));
        Assert.Equal(0x5678, registers.GetRegisterPair(Sm83RegisterPair.DE));
        Assert.Equal(0x9ABC, registers.GetRegisterPair(Sm83RegisterPair.HL));
        Assert.Equal(0xDEF0, registers.GetRegisterPair(Sm83RegisterPair.SP));

        registers.SetRegisterPair(Sm83RegisterPair.BC, 0x1111);
        registers.SetRegisterPair(Sm83RegisterPair.DE, 0x2222);
        registers.SetRegisterPair(Sm83RegisterPair.HL, 0x3333);
        registers.SetRegisterPair(Sm83RegisterPair.SP, 0x4444);

        Assert.Equal(0x1111, registers.BC);
        Assert.Equal(0x2222, registers.DE);
        Assert.Equal(0x3333, registers.HL);
        Assert.Equal(0x4444, registers.SP);
    }
}
