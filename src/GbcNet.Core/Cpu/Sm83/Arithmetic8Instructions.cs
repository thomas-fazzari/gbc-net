namespace GbcNet.Core.Cpu.Sm83;

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
    private const byte IncrementLOpcode = 0x2C;
    private const byte DecrementLOpcode = 0x2D;
    private const byte IncrementAOpcode = 0x3C;
    private const byte DecrementAOpcode = 0x3D;

    /// <summary>
    /// Low 4 bits used to detect H for INC and DEC r8.
    /// </summary>
    private const byte HalfCarryMask = 0x0F;

    private const byte NoOperandByteLength = 1;

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
        MapIncrementRegister(builder, IncrementLOpcode, Register8.L);
        MapDecrementRegister(builder, DecrementLOpcode, Register8.L);
        MapIncrementRegister(builder, IncrementAOpcode, Register8.A);
        MapDecrementRegister(builder, DecrementAOpcode, Register8.A);
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
            RegisterMachineCycles,
            (cpu, _, _) => IncrementRegister(cpu, register)
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
            RegisterMachineCycles,
            (cpu, _, _) => DecrementRegister(cpu, register)
        );
    }

    /// <summary>
    /// Increments the selected r8 register and updates the INC r8 flags.
    /// </summary>
    private static void IncrementRegister(Cpu cpu, Register8 register)
    {
        byte value = cpu.Registers.GetRegister(register);
        byte result = unchecked((byte)(value + 1));

        cpu.Registers.SetRegister(register, result);
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == HalfCarryMask);
    }

    /// <summary>
    /// Decrements the selected r8 register and updates the DEC r8 flags.
    /// </summary>
    private static void DecrementRegister(Cpu cpu, Register8 register)
    {
        byte value = cpu.Registers.GetRegister(register);
        byte result = unchecked((byte)(value - 1));

        cpu.Registers.SetRegister(register, result);
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, (value & HalfCarryMask) == 0);
    }
}
