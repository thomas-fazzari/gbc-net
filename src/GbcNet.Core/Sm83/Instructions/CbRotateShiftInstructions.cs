namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 CB-prefixed rotate and shift instructions.
/// </summary>
internal static class CbRotateShiftInstructions
{
    private const byte RotateShiftStartOpcode = 0x00;
    private const byte RotateShiftEndOpcode = 0x3F;

    /// <summary>
    /// Mask for the source bit moved into C by left rotations and shifts.
    /// </summary>
    private const byte HighBitMask = 0x80;

    /// <summary>
    /// Mask for the source bit moved into C by right rotations and shifts.
    /// </summary>
    private const byte LowBitMask = 0x01;

    /// <summary>
    /// Shift for the rotate or shift operation encoded in CB opcode bits 5-3.
    /// </summary>
    private const int OperationShift = 3;

    /// <summary>
    /// Mask for one decoded rotate or shift operation.
    /// </summary>
    private const byte OperationMask = 0x07;

    private static readonly RotateShiftExecutor[] _operations =
    [
        RotateLeftCircular,
        RotateRightCircular,
        RotateLeft,
        RotateRight,
        ShiftLeftArithmetic,
        ShiftRightArithmetic,
        Swap,
        ShiftRightLogical,
    ];

    /// <summary>
    /// Transforms one r8 operand value and reports the new C flag.
    /// </summary>
    private delegate byte RotateShiftExecutor(byte value, bool incomingCarry, out bool carry);

    /// <summary>
    /// Maps all CB rotate and shift instructions into the CB-prefixed opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        for (int opcode = RotateShiftStartOpcode; opcode <= RotateShiftEndOpcode; opcode++)
        {
            var opcodeByte = (byte)opcode;
            var operand = Register8Operands.DecodeSource(opcodeByte);
            var execute = DecodeOperation(opcodeByte);

            builder.MapPrefixed(opcodeByte, (cpu, _, _) => Execute(cpu, operand, execute));
        }
    }

    /// <summary>
    /// Executes a CB rotate or shift operation on one r8 operand.
    /// </summary>
    private static void Execute(Cpu cpu, Register8Operand operand, RotateShiftExecutor execute)
    {
        var value = Register8Operands.Read(cpu, operand);
        var incomingCarry = cpu.Registers.IsFlagSet(CpuFlag.Carry);
        var result = execute(value, incomingCarry, out var carry);

        Register8Operands.Write(cpu, operand, result);
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, carry);
    }

    /// <summary>
    /// Decodes the rotate or shift operation from CB opcode bits 5-3.
    /// </summary>
    private static RotateShiftExecutor DecodeOperation(byte opcode) =>
        _operations[(opcode >> OperationShift) & OperationMask];

    /// <summary>
    /// RLC: bit 7 moves into both bit 0 and C.
    /// </summary>
    private static byte RotateLeftCircular(byte value, bool _, out bool carry)
    {
        carry = (value & HighBitMask) != 0;
        return unchecked((byte)((value << 1) | (carry ? LowBitMask : 0)));
    }

    /// <summary>
    /// RRC: bit 0 moves into both bit 7 and C.
    /// </summary>
    private static byte RotateRightCircular(byte value, bool _, out bool carry)
    {
        carry = (value & LowBitMask) != 0;
        return (byte)((value >> 1) | (carry ? HighBitMask : 0));
    }

    /// <summary>
    /// RL: bit 7 moves into C, and old C moves into bit 0.
    /// </summary>
    private static byte RotateLeft(byte value, bool incomingCarry, out bool carry)
    {
        carry = (value & HighBitMask) != 0;
        return unchecked((byte)((value << 1) | (incomingCarry ? LowBitMask : 0)));
    }

    /// <summary>
    /// RR: bit 0 moves into C, and old C moves into bit 7.
    /// </summary>
    private static byte RotateRight(byte value, bool incomingCarry, out bool carry)
    {
        carry = (value & LowBitMask) != 0;
        return (byte)((value >> 1) | (incomingCarry ? HighBitMask : 0));
    }

    /// <summary>
    /// SLA: bit 7 moves into C, and zero moves into bit 0.
    /// </summary>
    private static byte ShiftLeftArithmetic(byte value, bool _, out bool carry)
    {
        carry = (value & HighBitMask) != 0;
        return unchecked((byte)(value << 1));
    }

    /// <summary>
    /// SRA: bit 0 moves into C, and bit 7 is preserved.
    /// </summary>
    private static byte ShiftRightArithmetic(byte value, bool _, out bool carry)
    {
        carry = (value & LowBitMask) != 0;
        return (byte)((value >> 1) | (value & HighBitMask));
    }

    /// <summary>
    /// SWAP: high and low nibbles are exchanged, and C is reset.
    /// </summary>
    private static byte Swap(byte value, bool _, out bool carry)
    {
        carry = false;
        return unchecked((byte)((value >> 4) | (value << 4)));
    }

    /// <summary>
    /// SRL: bit 0 moves into C, and zero moves into bit 7.
    /// </summary>
    private static byte ShiftRightLogical(byte value, bool _, out bool carry)
    {
        carry = (value & LowBitMask) != 0;
        return (byte)(value >> 1);
    }
}
