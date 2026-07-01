namespace GbcNet.Core.Sm83.Instructions;

internal static partial class Arithmetic8Instructions
{
    private static void MapIncrementRegister(
        OpcodeTableBuilder builder,
        byte opcode,
        Register8 register
    )
    {
        builder.MapNoOperand(opcode, cpu => IncrementRegister(cpu, register));
    }

    private static void MapDecrementRegister(
        OpcodeTableBuilder builder,
        byte opcode,
        Register8 register
    )
    {
        builder.MapNoOperand(opcode, cpu => DecrementRegister(cpu, register));
    }

    private static void IncrementRegister(Cpu cpu, Register8 register)
    {
        var value = cpu.Registers.GetRegister(register);
        cpu.Registers.SetRegister(register, IncrementByte(cpu, value));
    }

    private static void DecrementRegister(Cpu cpu, Register8 register)
    {
        var value = cpu.Registers.GetRegister(register);
        cpu.Registers.SetRegister(register, DecrementByte(cpu, value));
    }

    private static void IncrementAddressHl(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        var value = cpu.ReadBus(address);
        cpu.WriteBus(address, IncrementByte(cpu, value));
    }

    private static void DecrementAddressHl(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        var value = cpu.ReadBus(address);
        cpu.WriteBus(address, DecrementByte(cpu, value));
    }

    private static byte IncrementByte(Cpu cpu, byte value)
    {
        var result = unchecked((byte)(value + 1));

        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == HalfCarryMask);
        return result;
    }

    private static byte DecrementByte(Cpu cpu, byte value)
    {
        var result = unchecked((byte)(value - 1));

        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == 0);
        return result;
    }
}
