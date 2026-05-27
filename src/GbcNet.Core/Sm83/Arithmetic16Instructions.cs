namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 16-bit arithmetic instructions.
/// </summary>
internal static class Arithmetic16Instructions
{
    private const byte IncrementBcOpcode = 0x03;
    private const byte AddHlBcOpcode = 0x09;
    private const byte DecrementBcOpcode = 0x0B;
    private const byte IncrementDeOpcode = 0x13;
    private const byte AddHlDeOpcode = 0x19;
    private const byte DecrementDeOpcode = 0x1B;
    private const byte IncrementHlOpcode = 0x23;
    private const byte AddHlHlOpcode = 0x29;
    private const byte DecrementHlOpcode = 0x2B;
    private const byte IncrementSpOpcode = 0x33;
    private const byte AddHlSpOpcode = 0x39;
    private const byte DecrementSpOpcode = 0x3B;

    /// <summary>
    /// Low 12 bits used to detect the H flag for ADD HL, r16.
    /// </summary>
    private const ushort AddHlHalfCarryMask = 0x0FFF;

    private const byte NoOperandByteLength = 1;

    private const int AddHlRegisterPairMachineCycles = 2;
    private const int IncrementDecrementRegisterPairMachineCycles = 2;

    /// <summary>
    /// Maps implemented 16-bit arithmetic instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapIncrementRegisterPair(builder, IncrementBcOpcode, RegisterPair.BC);
        MapAddHlRegisterPair(builder, AddHlBcOpcode, RegisterPair.BC);
        MapDecrementRegisterPair(builder, DecrementBcOpcode, RegisterPair.BC);

        MapIncrementRegisterPair(builder, IncrementDeOpcode, RegisterPair.DE);
        MapAddHlRegisterPair(builder, AddHlDeOpcode, RegisterPair.DE);
        MapDecrementRegisterPair(builder, DecrementDeOpcode, RegisterPair.DE);

        MapIncrementRegisterPair(builder, IncrementHlOpcode, RegisterPair.HL);
        MapAddHlRegisterPair(builder, AddHlHlOpcode, RegisterPair.HL);
        MapDecrementRegisterPair(builder, DecrementHlOpcode, RegisterPair.HL);

        MapIncrementRegisterPair(builder, IncrementSpOpcode, RegisterPair.SP);
        MapAddHlRegisterPair(builder, AddHlSpOpcode, RegisterPair.SP);
        MapDecrementRegisterPair(builder, DecrementSpOpcode, RegisterPair.SP);
    }

    /// <summary>
    /// Maps an ADD HL, r16 instruction, which preserves Z and updates N, H, and C.
    /// </summary>
    private static void MapAddHlRegisterPair(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            AddHlRegisterPairMachineCycles,
            (cpu, _, _) => AddHlRegisterPair(cpu, registerPair)
        );
    }

    /// <summary>
    /// Maps an INC r16 instruction, which wraps at 16 bits and leaves flags unchanged.
    /// </summary>
    private static void MapIncrementRegisterPair(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            IncrementDecrementRegisterPairMachineCycles,
            (cpu, _, _) => IncrementRegisterPair(cpu, registerPair)
        );
    }

    /// <summary>
    /// Maps a DEC r16 instruction, which wraps at 16 bits and leaves flags unchanged.
    /// </summary>
    private static void MapDecrementRegisterPair(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            IncrementDecrementRegisterPairMachineCycles,
            (cpu, _, _) => DecrementRegisterPair(cpu, registerPair)
        );
    }

    /// <summary>
    /// Adds the selected r16 register pair to HL and updates the ADD HL, r16 flags.
    /// </summary>
    private static void AddHlRegisterPair(Cpu cpu, RegisterPair registerPair)
    {
        ushort left = cpu.Registers.HL;
        ushort right = cpu.Registers.GetRegisterPair(registerPair);
        int result = left + right;

        cpu.Registers.HL = unchecked((ushort)result);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, HasAddHlHalfCarry(left, right));
        cpu.Registers.SetFlag(CpuFlag.Carry, result > ushort.MaxValue);
    }

    /// <summary>
    /// Increments the selected r16 register pair with 16-bit wraparound.
    /// </summary>
    private static void IncrementRegisterPair(Cpu cpu, RegisterPair registerPair)
    {
        ushort value = cpu.Registers.GetRegisterPair(registerPair);
        cpu.Registers.SetRegisterPair(registerPair, unchecked((ushort)(value + 1)));
    }

    /// <summary>
    /// Decrements the selected r16 register pair with 16-bit wraparound.
    /// </summary>
    private static void DecrementRegisterPair(Cpu cpu, RegisterPair registerPair)
    {
        ushort value = cpu.Registers.GetRegisterPair(registerPair);
        cpu.Registers.SetRegisterPair(registerPair, unchecked((ushort)(value - 1)));
    }

    /// <summary>
    /// Returns whether ADD HL, r16 carries from bit 11 into bit 12.
    /// </summary>
    private static bool HasAddHlHalfCarry(ushort left, ushort right) =>
        (left & AddHlHalfCarryMask) + (right & AddHlHalfCarryMask) > AddHlHalfCarryMask;
}
