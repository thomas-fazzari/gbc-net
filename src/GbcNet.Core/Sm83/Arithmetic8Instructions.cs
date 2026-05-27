namespace GbcNet.Core.Sm83;

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

    private const byte NoOperandByteLength = 1;

    private const int AddressHlMachineCycles = 3;
    private const int AddAccumulatorAddressHlMachineCycles = 2;
    private const int AddAccumulatorRegisterMachineCycles = 1;
    private const int ComplementAccumulatorMachineCycles = 1;
    private const int DecimalAdjustAccumulatorMachineCycles = 1;
    private const int RegisterMachineCycles = 1;

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
        builder.Map(
            DecimalAdjustAccumulatorOpcode,
            NoOperandByteLength,
            static (cpu, _, _) =>
            {
                DecimalAdjustAccumulator(cpu);
                return DecimalAdjustAccumulatorMachineCycles;
            }
        );
        builder.Map(
            ComplementAccumulatorOpcode,
            NoOperandByteLength,
            static (cpu, _, _) =>
            {
                ComplementAccumulator(cpu);
                return ComplementAccumulatorMachineCycles;
            }
        );
        MapIncrementRegister(builder, IncrementLOpcode, Register8.L);
        MapDecrementRegister(builder, DecrementLOpcode, Register8.L);
        MapIncrementAddressHl(builder, IncrementAddressHlOpcode);
        MapDecrementAddressHl(builder, DecrementAddressHlOpcode);
        MapIncrementRegister(builder, IncrementAOpcode, Register8.A);
        MapDecrementRegister(builder, DecrementAOpcode, Register8.A);
        MapAddAccumulatorRegisterOperand(builder);
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
        builder.Map(
            opcode,
            NoOperandByteLength,
            (cpu, _, _) =>
            {
                IncrementRegister(cpu, register);
                return RegisterMachineCycles;
            }
        );
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
        builder.Map(
            opcode,
            NoOperandByteLength,
            (cpu, _, _) =>
            {
                DecrementRegister(cpu, register);
                return RegisterMachineCycles;
            }
        );
    }

    /// <summary>
    /// Maps INC [HL], which updates Z, N, and H while preserving C.
    /// </summary>
    private static void MapIncrementAddressHl(OpcodeTableBuilder builder, byte opcode)
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            static (cpu, _, _) =>
            {
                IncrementAddressHl(cpu);
                return AddressHlMachineCycles;
            }
        );
    }

    /// <summary>
    /// Maps DEC [HL], which updates Z, N, and H while preserving C.
    /// </summary>
    private static void MapDecrementAddressHl(OpcodeTableBuilder builder, byte opcode)
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            static (cpu, _, _) =>
            {
                DecrementAddressHl(cpu);
                return AddressHlMachineCycles;
            }
        );
    }

    /// <summary>
    /// Maps ADD A, r8 instructions, which reset N and update Z, H, and C.
    /// </summary>
    private static void MapAddAccumulatorRegisterOperand(OpcodeTableBuilder builder)
    {
        for (
            int opcode = AddAccumulatorRegisterOperandStartOpcode;
            opcode <= AddAccumulatorRegisterOperandEndOpcode;
            opcode++
        )
        {
            byte opcodeByte = (byte)opcode;
            Register8Operand source = Register8Operands.DecodeSource(opcodeByte);
            builder.Map(
                opcodeByte,
                NoOperandByteLength,
                (cpu, _, _) => AddAccumulatorRegisterOperand(cpu, source)
            );
        }
    }

    /// <summary>
    /// Increments the selected r8 register and updates the INC r8 flags.
    /// </summary>
    private static void IncrementRegister(Cpu cpu, Register8 register)
    {
        byte value = cpu.Registers.GetRegister(register);
        cpu.Registers.SetRegister(register, IncrementByte(cpu, value));
    }

    /// <summary>
    /// Decrements the selected r8 register and updates the DEC r8 flags.
    /// </summary>
    private static void DecrementRegister(Cpu cpu, Register8 register)
    {
        byte value = cpu.Registers.GetRegister(register);
        cpu.Registers.SetRegister(register, DecrementByte(cpu, value));
    }

    /// <summary>
    /// Increments the byte at HL and updates the INC [HL] flags.
    /// </summary>
    private static void IncrementAddressHl(Cpu cpu)
    {
        ushort address = cpu.Registers.HL;
        byte value = cpu.ReadByte(address);
        cpu.WriteByte(address, IncrementByte(cpu, value));
    }

    /// <summary>
    /// Decrements the byte at HL and updates the DEC [HL] flags.
    /// </summary>
    private static void DecrementAddressHl(Cpu cpu)
    {
        ushort address = cpu.Registers.HL;
        byte value = cpu.ReadByte(address);
        cpu.WriteByte(address, DecrementByte(cpu, value));
    }

    /// <summary>
    /// Adds the selected r8 operand to A and updates ADD A, r8 flags.
    /// </summary>
    private static int AddAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        byte value = Register8Operands.Read(cpu, source);
        AddAccumulator(cpu, value);

        return Register8Operands.UsesMemory(source)
            ? AddAccumulatorAddressHlMachineCycles
            : AddAccumulatorRegisterMachineCycles;
    }

    /// <summary>
    /// Increments one byte and applies INC r8/[HL] flag effects.
    /// </summary>
    private static byte IncrementByte(Cpu cpu, byte value)
    {
        byte result = unchecked((byte)(value + 1));

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
        byte result = unchecked((byte)(value - 1));

        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == 0);
        return result;
    }

    /// <summary>
    /// Adds one byte to A and applies ADD A flag effects.
    /// </summary>
    private static void AddAccumulator(Cpu cpu, byte value)
    {
        byte accumulator = cpu.Registers.A;
        int result = accumulator + value;

        cpu.Registers.A = unchecked((byte)result);
        cpu.Registers.SetFlag(CpuFlag.Zero, cpu.Registers.A == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, HasLowNibbleAddCarry(accumulator, value));
        cpu.Registers.SetFlag(CpuFlag.Carry, result > byte.MaxValue);
    }

    /// <summary>
    /// Returns whether adding two bytes carries from bit 3 into bit 4.
    /// </summary>
    private static bool HasLowNibbleAddCarry(byte left, byte right) =>
        (left & HalfCarryMask) + (right & HalfCarryMask) > HalfCarryMask;

    /// <summary>
    /// Adjusts A to a packed BCD result using the N, H, and C flags from the previous operation.
    /// </summary>
    private static void DecimalAdjustAccumulator(Cpu cpu)
    {
        int value = cpu.Registers.A;
        bool subtract = cpu.Registers.IsFlagSet(CpuFlag.Subtract);
        bool carry = cpu.Registers.IsFlagSet(CpuFlag.Carry);
        bool halfCarry = cpu.Registers.IsFlagSet(CpuFlag.HalfCarry);

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

        byte result = unchecked((byte)value);
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
