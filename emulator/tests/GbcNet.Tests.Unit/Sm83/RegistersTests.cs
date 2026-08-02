// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

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

        registers.AF.Should().Be(0x1230);
        registers.BC.Should().Be(0x4567);
        registers.DE.Should().Be(0x89AB);
        registers.HL.Should().Be(0xCDEF);
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

        registers.A.Should().Be(0x12);
        registers.F.Should().Be(0x30);
        registers.B.Should().Be(0x45);
        registers.C.Should().Be(0x67);
        registers.D.Should().Be(0x89);
        registers.E.Should().Be(0xAB);
        registers.H.Should().Be(0xCD);
        registers.L.Should().Be(0xEF);
    }

    [Fact]
    public void F_MasksUnusedLowerNibble()
    {
        Registers registers = new() { F = 0xFF };

        registers.F.Should().Be(0xF0);
    }

    [Fact]
    public void SetFlag_UpdatesOnlySelectedFlag()
    {
        Registers registers = new();

        registers.SetFlag(CpuFlag.Zero, isSet: true);
        registers.SetFlag(CpuFlag.Carry, isSet: true);
        registers.SetFlag(CpuFlag.Zero, isSet: false);

        registers.IsFlagSet(CpuFlag.Zero).Should().BeFalse();
        registers.IsFlagSet(CpuFlag.Subtract).Should().BeFalse();
        registers.IsFlagSet(CpuFlag.HalfCarry).Should().BeFalse();
        registers.IsFlagSet(CpuFlag.Carry).Should().BeTrue();
        registers.F.Should().Be(0x10);
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

        registers.GetRegister(Register8.B).Should().Be(0x34);
        registers.GetRegister(Register8.C).Should().Be(0x56);
        registers.GetRegister(Register8.D).Should().Be(0x78);
        registers.GetRegister(Register8.E).Should().Be(0x9A);
        registers.GetRegister(Register8.H).Should().Be(0xBC);
        registers.GetRegister(Register8.L).Should().Be(0xDE);
        registers.GetRegister(Register8.A).Should().Be(0x12);

        registers.SetRegister(Register8.B, 0x01);
        registers.SetRegister(Register8.C, 0x23);
        registers.SetRegister(Register8.D, 0x45);
        registers.SetRegister(Register8.E, 0x67);
        registers.SetRegister(Register8.H, 0x89);
        registers.SetRegister(Register8.L, 0xAB);
        registers.SetRegister(Register8.A, 0xCD);

        registers.B.Should().Be(0x01);
        registers.C.Should().Be(0x23);
        registers.D.Should().Be(0x45);
        registers.E.Should().Be(0x67);
        registers.H.Should().Be(0x89);
        registers.L.Should().Be(0xAB);
        registers.A.Should().Be(0xCD);
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

        registers.GetRegisterPair(RegisterPair.BC).Should().Be(0x1234);
        registers.GetRegisterPair(RegisterPair.DE).Should().Be(0x5678);
        registers.GetRegisterPair(RegisterPair.HL).Should().Be(0x9ABC);
        registers.GetRegisterPair(RegisterPair.SP).Should().Be(0xDEF0);

        registers.SetRegisterPair(RegisterPair.BC, 0x1111);
        registers.SetRegisterPair(RegisterPair.DE, 0x2222);
        registers.SetRegisterPair(RegisterPair.HL, 0x3333);
        registers.SetRegisterPair(RegisterPair.SP, 0x4444);

        registers.BC.Should().Be(0x1111);
        registers.DE.Should().Be(0x2222);
        registers.HL.Should().Be(0x3333);
        registers.SP.Should().Be(0x4444);
    }
}
