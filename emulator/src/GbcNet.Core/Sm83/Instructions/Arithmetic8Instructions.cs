// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 8-bit arithmetic instructions.
/// </summary>
internal static partial class Arithmetic8Instructions
{
    private const byte IncrementBOpcode = 0x04;
    private const byte DecrementBOpcode = 0x05;
    private const byte IncrementCOpcode = 0x0C;
    private const byte DecrementCOpcode = 0x0D;
    private const byte IncrementDOpcode = 0x14;
    private const byte DecrementDOpcode = 0x15;
    private const byte IncrementEOpcode = 0x1C;
    private const byte DecrementEOpcode = 0x1D;
    private const byte IncrementHOpcode = 0x24;
    private const byte DecrementHOpcode = 0x25;
    private const byte DecimalAdjustAccumulatorOpcode = 0x27;
    private const byte ComplementAccumulatorOpcode = 0x2F;
    private const byte IncrementLOpcode = 0x2C;
    private const byte DecrementLOpcode = 0x2D;
    private const byte IncrementAddressHlOpcode = 0x34;
    private const byte DecrementAddressHlOpcode = 0x35;
    private const byte IncrementAOpcode = 0x3C;
    private const byte DecrementAOpcode = 0x3D;
    private const byte AddAccumulatorRegisterOperandStartOpcode = 0x80;
    private const byte AddAccumulatorRegisterOperandEndOpcode = 0x87;
    private const byte AddWithCarryAccumulatorRegisterOperandStartOpcode = 0x88;
    private const byte AddWithCarryAccumulatorRegisterOperandEndOpcode = 0x8F;
    private const byte SubtractAccumulatorRegisterOperandStartOpcode = 0x90;
    private const byte SubtractAccumulatorRegisterOperandEndOpcode = 0x97;
    private const byte SubtractWithCarryAccumulatorRegisterOperandStartOpcode = 0x98;
    private const byte SubtractWithCarryAccumulatorRegisterOperandEndOpcode = 0x9F;
    private const byte AndAccumulatorRegisterOperandStartOpcode = 0xA0;
    private const byte AndAccumulatorRegisterOperandEndOpcode = 0xA7;
    private const byte XorAccumulatorRegisterOperandStartOpcode = 0xA8;
    private const byte XorAccumulatorRegisterOperandEndOpcode = 0xAF;
    private const byte OrAccumulatorRegisterOperandStartOpcode = 0xB0;
    private const byte OrAccumulatorRegisterOperandEndOpcode = 0xB7;
    private const byte CompareAccumulatorRegisterOperandStartOpcode = 0xB8;
    private const byte CompareAccumulatorRegisterOperandEndOpcode = 0xBF;
    private const byte AddAccumulatorImmediateOpcode = 0xC6;
    private const byte AddWithCarryAccumulatorImmediateOpcode = 0xCE;
    private const byte SubtractAccumulatorImmediateOpcode = 0xD6;
    private const byte SubtractWithCarryAccumulatorImmediateOpcode = 0xDE;
    private const byte AndAccumulatorImmediateOpcode = 0xE6;
    private const byte XorAccumulatorImmediateOpcode = 0xEE;
    private const byte OrAccumulatorImmediateOpcode = 0xF6;
    private const byte CompareAccumulatorImmediateOpcode = 0xFE;

    private const byte HalfCarryMask = 0x0F;

    /// <summary>
    /// Maps implemented 8-bit arithmetic instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapIncrementRegister(builder, IncrementBOpcode, Register8.B);
        MapDecrementRegister(builder, DecrementBOpcode, Register8.B);
        MapIncrementRegister(builder, IncrementCOpcode, Register8.C);
        MapDecrementRegister(builder, DecrementCOpcode, Register8.C);
        MapIncrementRegister(builder, IncrementDOpcode, Register8.D);
        MapDecrementRegister(builder, DecrementDOpcode, Register8.D);
        MapIncrementRegister(builder, IncrementEOpcode, Register8.E);
        MapDecrementRegister(builder, DecrementEOpcode, Register8.E);
        MapIncrementRegister(builder, IncrementHOpcode, Register8.H);
        MapDecrementRegister(builder, DecrementHOpcode, Register8.H);
        builder.MapNoOperand(DecimalAdjustAccumulatorOpcode, DecimalAdjustAccumulator);
        builder.MapNoOperand(ComplementAccumulatorOpcode, ComplementAccumulator);
        MapIncrementRegister(builder, IncrementLOpcode, Register8.L);
        MapDecrementRegister(builder, DecrementLOpcode, Register8.L);
        builder.MapNoOperand(IncrementAddressHlOpcode, IncrementAddressHl);
        builder.MapNoOperand(DecrementAddressHlOpcode, DecrementAddressHl);
        MapIncrementRegister(builder, IncrementAOpcode, Register8.A);
        MapDecrementRegister(builder, DecrementAOpcode, Register8.A);
        MapAccumulatorRegisterOperand(
            builder,
            AddAccumulatorRegisterOperandStartOpcode,
            AddAccumulatorRegisterOperandEndOpcode,
            AddAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            AddWithCarryAccumulatorRegisterOperandStartOpcode,
            AddWithCarryAccumulatorRegisterOperandEndOpcode,
            AddWithCarryAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            SubtractAccumulatorRegisterOperandStartOpcode,
            SubtractAccumulatorRegisterOperandEndOpcode,
            SubtractAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            SubtractWithCarryAccumulatorRegisterOperandStartOpcode,
            SubtractWithCarryAccumulatorRegisterOperandEndOpcode,
            SubtractWithCarryAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            AndAccumulatorRegisterOperandStartOpcode,
            AndAccumulatorRegisterOperandEndOpcode,
            AndAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            XorAccumulatorRegisterOperandStartOpcode,
            XorAccumulatorRegisterOperandEndOpcode,
            XorAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            OrAccumulatorRegisterOperandStartOpcode,
            OrAccumulatorRegisterOperandEndOpcode,
            OrAccumulatorRegisterOperand
        );
        MapAccumulatorRegisterOperand(
            builder,
            CompareAccumulatorRegisterOperandStartOpcode,
            CompareAccumulatorRegisterOperandEndOpcode,
            CompareAccumulatorRegisterOperand
        );
        builder.MapImmediate8(
            AddAccumulatorImmediateOpcode,
            static (cpu, value) => AddAccumulator(cpu, value, carry: 0)
        );
        builder.MapImmediate8(AddWithCarryAccumulatorImmediateOpcode, AddWithCarryAccumulator);
        builder.MapImmediate8(
            SubtractAccumulatorImmediateOpcode,
            static (cpu, value) => SubtractAccumulator(cpu, value, borrow: 0)
        );
        builder.MapImmediate8(
            SubtractWithCarryAccumulatorImmediateOpcode,
            SubtractWithCarryAccumulator
        );
        builder.MapImmediate8(AndAccumulatorImmediateOpcode, AndAccumulator);
        builder.MapImmediate8(XorAccumulatorImmediateOpcode, XorAccumulator);
        builder.MapImmediate8(OrAccumulatorImmediateOpcode, OrAccumulator);
        builder.MapImmediate8(CompareAccumulatorImmediateOpcode, CompareAccumulator);
    }
}
