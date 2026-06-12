namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 8-bit arithmetic instructions.
/// </summary>
internal static class Arithmetic8Instructions
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

    /// <summary>
    /// Executes one A,r8 accumulator operation after the source operand has been decoded.
    /// </summary>
    private delegate void AccumulatorRegisterOperandExecutor(Cpu cpu, Register8Operand source);

    /// <summary>
    /// Applies one A,imm8 accumulator operation after the immediate byte has been fetched.
    /// </summary>
    private delegate void AccumulatorImmediateOperandExecutor(Cpu cpu, byte value);

    /// <summary>
    /// Low 4 bits used to detect H for 8-bit arithmetic.
    /// </summary>
    private const byte HalfCarryMask = 0x0F;

    /// <summary>
    /// Low BCD digit adjustment used by DAA.
    /// </summary>
    private const byte DecimalLowAdjust = 0x06;

    /// <summary>
    /// Maximum value of one packed BCD digit.
    /// </summary>
    private const byte DecimalDigitMax = 9;

    /// <summary>
    /// High BCD digit adjustment used by DAA.
    /// </summary>
    private const byte DecimalHighAdjust = 0x60;

    /// <summary>
    /// Highest post-low-adjust value that does not require a high BCD carry.
    /// </summary>
    private const byte DecimalHighCarryThreshold = 0x9F;

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
        MapIncrementAddressHl(builder, IncrementAddressHlOpcode);
        MapDecrementAddressHl(builder, DecrementAddressHlOpcode);
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
        MapAccumulatorImmediateOperand(
            builder,
            AddAccumulatorImmediateOpcode,
            static (cpu, value) => AddAccumulator(cpu, value, carry: 0)
        );
        MapAccumulatorImmediateOperand(
            builder,
            AddWithCarryAccumulatorImmediateOpcode,
            AddWithCarryAccumulator
        );
        MapAccumulatorImmediateOperand(
            builder,
            SubtractAccumulatorImmediateOpcode,
            static (cpu, value) => SubtractAccumulator(cpu, value, borrow: 0)
        );
        MapAccumulatorImmediateOperand(
            builder,
            SubtractWithCarryAccumulatorImmediateOpcode,
            SubtractWithCarryAccumulator
        );
        MapAccumulatorImmediateOperand(builder, AndAccumulatorImmediateOpcode, AndAccumulator);
        MapAccumulatorImmediateOperand(builder, XorAccumulatorImmediateOpcode, XorAccumulator);
        MapAccumulatorImmediateOperand(builder, OrAccumulatorImmediateOpcode, OrAccumulator);
        MapAccumulatorImmediateOperand(
            builder,
            CompareAccumulatorImmediateOpcode,
            CompareAccumulator
        );
    }

    /// <summary>
    /// Maps an INC r8 instruction, which updates Z, N, and H while preserving C.
    /// </summary>
    private static void MapIncrementRegister(
        OpcodeTableBuilder builder,
        byte opcode,
        Register8 register
    )
    {
        builder.MapNoOperand(opcode, cpu => IncrementRegister(cpu, register));
    }

    /// <summary>
    /// Maps a DEC r8 instruction, which updates Z, N, and H while preserving C.
    /// </summary>
    private static void MapDecrementRegister(
        OpcodeTableBuilder builder,
        byte opcode,
        Register8 register
    )
    {
        builder.MapNoOperand(opcode, cpu => DecrementRegister(cpu, register));
    }

    /// <summary>
    /// Maps INC [HL], which updates Z, N, and H while preserving C.
    /// </summary>
    private static void MapIncrementAddressHl(OpcodeTableBuilder builder, byte opcode)
    {
        builder.MapNoOperand(opcode, IncrementAddressHl);
    }

    /// <summary>
    /// Maps DEC [HL], which updates Z, N, and H while preserving C.
    /// </summary>
    private static void MapDecrementAddressHl(OpcodeTableBuilder builder, byte opcode)
    {
        builder.MapNoOperand(opcode, DecrementAddressHl);
    }

    /// <summary>
    /// Increments the selected r8 register and updates the INC r8 flags.
    /// </summary>
    private static void IncrementRegister(Cpu cpu, Register8 register)
    {
        var value = cpu.Registers.GetRegister(register);
        cpu.Registers.SetRegister(register, IncrementByte(cpu, value));
    }

    /// <summary>
    /// Decrements the selected r8 register and updates the DEC r8 flags.
    /// </summary>
    private static void DecrementRegister(Cpu cpu, Register8 register)
    {
        var value = cpu.Registers.GetRegister(register);
        cpu.Registers.SetRegister(register, DecrementByte(cpu, value));
    }

    /// <summary>
    /// Increments the byte at HL and updates the INC [HL] flags.
    /// </summary>
    private static void IncrementAddressHl(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        var value = cpu.ReadBus(address);
        cpu.WriteBus(address, IncrementByte(cpu, value));
    }

    /// <summary>
    /// Decrements the byte at HL and updates the DEC [HL] flags.
    /// </summary>
    private static void DecrementAddressHl(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        var value = cpu.ReadBus(address);
        cpu.WriteBus(address, DecrementByte(cpu, value));
    }

    /// <summary>
    /// Adds the selected r8 operand to A and updates ADD A, r8 flags.
    /// </summary>
    private static void AddAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        AddAccumulator(cpu, value, carry: 0);
    }

    /// <summary>
    /// Adds the selected r8 operand and C flag to A, then updates ADC A, r8 flags.
    /// </summary>
    private static void AddWithCarryAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        AddWithCarryAccumulator(cpu, value);
    }

    /// <summary>
    /// Subtracts the selected r8 operand from A and updates SUB A, r8 flags.
    /// </summary>
    private static void SubtractAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        SubtractAccumulator(cpu, value, borrow: 0);
    }

    /// <summary>
    /// Subtracts the selected r8 operand and C flag from A, then updates SBC A, r8 flags.
    /// </summary>
    private static void SubtractWithCarryAccumulatorRegisterOperand(
        Cpu cpu,
        Register8Operand source
    )
    {
        var value = Register8Operands.Read(cpu, source);
        SubtractWithCarryAccumulator(cpu, value);
    }

    /// <summary>
    /// ANDs the selected r8 operand with A and updates AND A, r8 flags.
    /// </summary>
    private static void AndAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        AndAccumulator(cpu, value);
    }

    /// <summary>
    /// XORs the selected r8 operand with A and updates XOR A, r8 flags.
    /// </summary>
    private static void XorAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        XorAccumulator(cpu, value);
    }

    /// <summary>
    /// Applies bitwise OR between the selected r8 operand and A, then updates OR A, r8 flags.
    /// </summary>
    private static void OrAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        OrAccumulator(cpu, value);
    }

    /// <summary>
    /// Compares the selected r8 operand with A and updates CP A, r8 flags without changing A.
    /// </summary>
    private static void CompareAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        CompareAccumulator(cpu, value);
    }

    /// <summary>
    /// Increments one byte and applies INC r8/[HL] flag effects.
    /// </summary>
    private static byte IncrementByte(Cpu cpu, byte value)
    {
        var result = unchecked((byte)(value + 1));

        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == HalfCarryMask);
        return result;
    }

    /// <summary>
    /// Decrements one byte and applies DEC r8/[HL] flag effects.
    /// </summary>
    private static byte DecrementByte(Cpu cpu, byte value)
    {
        var result = unchecked((byte)(value - 1));

        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == 0);
        return result;
    }

    /// <summary>
    /// Adds one byte and an optional carry to A, then applies ADD/ADC A flag effects.
    /// </summary>
    private static void AddAccumulator(Cpu cpu, byte value, int carry)
    {
        var accumulator = cpu.Registers.A;
        var result = accumulator + value + carry;

        cpu.Registers.A = unchecked((byte)result);
        cpu.Registers.SetFlag(CpuFlag.Zero, cpu.Registers.A == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, HasLowNibbleAddCarry(accumulator, value, carry));
        cpu.Registers.SetFlag(CpuFlag.Carry, result > byte.MaxValue);
    }

    /// <summary>
    /// Adds one byte and the C flag to A, then applies ADC A flag effects.
    /// </summary>
    private static void AddWithCarryAccumulator(Cpu cpu, byte value)
    {
        AddAccumulator(cpu, value, cpu.Registers.IsFlagSet(CpuFlag.Carry) ? 1 : 0);
    }

    /// <summary>
    /// Returns whether adding two bytes and a carry input carries from bit 3 into bit 4.
    /// </summary>
    private static bool HasLowNibbleAddCarry(byte left, byte right, int carry) =>
        (left & HalfCarryMask) + (right & HalfCarryMask) + carry > HalfCarryMask;

    /// <summary>
    /// Subtracts one byte and an optional borrow from A, then applies SUB/SBC A flag effects.
    /// </summary>
    private static void SubtractAccumulator(Cpu cpu, byte value, int borrow)
    {
        var accumulator = cpu.Registers.A;
        var result = accumulator - value - borrow;

        cpu.Registers.A = unchecked((byte)result);
        cpu.Registers.SetFlag(CpuFlag.Zero, cpu.Registers.A == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(
            CpuFlag.HalfCarry,
            HasLowNibbleSubtractionBorrow(accumulator, value, borrow)
        );
        cpu.Registers.SetFlag(CpuFlag.Carry, result < 0);
    }

    /// <summary>
    /// Subtracts one byte and the C flag from A, then applies SBC A flag effects.
    /// </summary>
    private static void SubtractWithCarryAccumulator(Cpu cpu, byte value)
    {
        SubtractAccumulator(cpu, value, cpu.Registers.IsFlagSet(CpuFlag.Carry) ? 1 : 0);
    }

    /// <summary>
    /// Returns whether subtracting a byte and borrow input borrows from bit 4.
    /// </summary>
    private static bool HasLowNibbleSubtractionBorrow(byte left, byte right, int borrow) =>
        (left & HalfCarryMask) < (right & HalfCarryMask) + borrow;

    /// <summary>
    /// ANDs one byte with A, then applies AND A flag effects.
    /// </summary>
    private static void AndAccumulator(Cpu cpu, byte value)
    {
        var result = (byte)(cpu.Registers.A & value);

        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: false);
    }

    /// <summary>
    /// XORs one byte with A, then applies XOR A flag effects.
    /// </summary>
    private static void XorAccumulator(Cpu cpu, byte value)
    {
        var result = (byte)(cpu.Registers.A ^ value);

        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: false);
    }

    /// <summary>
    /// Applies bitwise OR with A, then applies OR A flag effects.
    /// </summary>
    private static void OrAccumulator(Cpu cpu, byte value)
    {
        var result = (byte)(cpu.Registers.A | value);

        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: false);
    }

    /// <summary>
    /// Compares one byte with A by applying SUB A flag effects without storing the result.
    /// </summary>
    private static void CompareAccumulator(Cpu cpu, byte value)
    {
        var accumulator = cpu.Registers.A;
        var result = accumulator - value;

        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(
            CpuFlag.HalfCarry,
            HasLowNibbleSubtractionBorrow(accumulator, value, borrow: 0)
        );
        cpu.Registers.SetFlag(CpuFlag.Carry, result < 0);
    }

    /// <summary>
    /// Maps an inclusive A,r8 opcode range using the low three opcode bits as source operand.
    /// </summary>
    private static void MapAccumulatorRegisterOperand(
        OpcodeTableBuilder builder,
        byte startOpcode,
        byte endOpcode,
        AccumulatorRegisterOperandExecutor execute
    )
    {
        for (int opcode = startOpcode; opcode <= endOpcode; opcode++)
        {
            var opcodeByte = (byte)opcode;
            var source = Register8Operands.DecodeSource(opcodeByte);
            builder.MapNoOperand(opcodeByte, cpu => execute(cpu, source));
        }
    }

    /// <summary>
    /// Maps an A,imm8 instruction whose operand byte is fetched after the opcode.
    /// </summary>
    private static void MapAccumulatorImmediateOperand(
        OpcodeTableBuilder builder,
        byte opcode,
        AccumulatorImmediateOperandExecutor execute
    )
    {
        builder.MapImmediate8(opcode, (cpu, value) => execute(cpu, value));
    }

    /// <summary>
    /// Adjusts A to a packed BCD result using the N, H, and C flags from the previous operation.
    /// </summary>
    private static void DecimalAdjustAccumulator(Cpu cpu)
    {
        int value = cpu.Registers.A;
        var subtract = cpu.Registers.IsFlagSet(CpuFlag.Subtract);
        var carry = cpu.Registers.IsFlagSet(CpuFlag.Carry);
        var halfCarry = cpu.Registers.IsFlagSet(CpuFlag.HalfCarry);

        if (subtract)
        {
            if (halfCarry)
            {
                value -= DecimalLowAdjust;
            }

            if (carry)
            {
                value -= DecimalHighAdjust;
            }
        }
        else
        {
            if (halfCarry || (value & HalfCarryMask) > DecimalDigitMax)
            {
                value += DecimalLowAdjust;
            }

            if (carry || value > DecimalHighCarryThreshold)
            {
                value += DecimalHighAdjust;
                carry = true;
            }
        }

        var result = unchecked((byte)value);
        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, carry);
    }

    /// <summary>
    /// Complements every bit in A, sets N and H, and preserves Z and C.
    /// </summary>
    private static void ComplementAccumulator(Cpu cpu)
    {
        cpu.Registers.A = (byte)~cpu.Registers.A;
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: true);
    }
}
