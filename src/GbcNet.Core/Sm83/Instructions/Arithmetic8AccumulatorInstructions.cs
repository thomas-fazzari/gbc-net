namespace GbcNet.Core.Sm83.Instructions;

internal static partial class Arithmetic8Instructions
{
    private delegate void AccumulatorRegisterOperandExecutor(Cpu cpu, Register8Operand source);

    private static void AddAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        AddAccumulator(cpu, value, carry: 0);
    }

    private static void AddWithCarryAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        AddWithCarryAccumulator(cpu, value);
    }

    private static void SubtractAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        SubtractAccumulator(cpu, value, borrow: 0);
    }

    private static void SubtractWithCarryAccumulatorRegisterOperand(
        Cpu cpu,
        Register8Operand source
    )
    {
        var value = Register8Operands.Read(cpu, source);
        SubtractWithCarryAccumulator(cpu, value);
    }

    private static void AndAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        AndAccumulator(cpu, value);
    }

    private static void XorAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        XorAccumulator(cpu, value);
    }

    private static void OrAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        OrAccumulator(cpu, value);
    }

    private static void CompareAccumulatorRegisterOperand(Cpu cpu, Register8Operand source)
    {
        var value = Register8Operands.Read(cpu, source);
        CompareAccumulator(cpu, value);
    }

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

    private static void AddWithCarryAccumulator(Cpu cpu, byte value)
    {
        AddAccumulator(cpu, value, cpu.Registers.IsFlagSet(CpuFlag.Carry) ? 1 : 0);
    }

    private static bool HasLowNibbleAddCarry(byte left, byte right, int carry) =>
        (left & HalfCarryMask) + (right & HalfCarryMask) + carry > HalfCarryMask;

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

    private static void SubtractWithCarryAccumulator(Cpu cpu, byte value)
    {
        SubtractAccumulator(cpu, value, cpu.Registers.IsFlagSet(CpuFlag.Carry) ? 1 : 0);
    }

    private static bool HasLowNibbleSubtractionBorrow(byte left, byte right, int borrow) =>
        (left & HalfCarryMask) < (right & HalfCarryMask) + borrow;

    private static void AndAccumulator(Cpu cpu, byte value)
    {
        var result = (byte)(cpu.Registers.A & value);

        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: false);
    }

    private static void XorAccumulator(Cpu cpu, byte value)
    {
        var result = (byte)(cpu.Registers.A ^ value);

        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: false);
    }

    private static void OrAccumulator(Cpu cpu, byte value)
    {
        var result = (byte)(cpu.Registers.A | value);

        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: false);
    }

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
}
