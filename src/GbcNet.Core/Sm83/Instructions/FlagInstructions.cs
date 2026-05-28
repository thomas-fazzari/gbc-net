namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 instructions that manipulate CPU flags.
/// </summary>
internal static class FlagInstructions
{
    private const byte SetCarryFlagOpcode = 0x37;
    private const byte ComplementCarryFlagOpcode = 0x3F;

    private const byte NoOperandByteLength = 1;

    /// <summary>
    /// Maps implemented flag instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(
            SetCarryFlagOpcode,
            NoOperandByteLength,
            static (cpu, _, _) => SetCarryFlag(cpu)
        );
        builder.Map(
            ComplementCarryFlagOpcode,
            NoOperandByteLength,
            static (cpu, _, _) => ComplementCarryFlag(cpu)
        );
    }

    /// <summary>
    /// Executes SCF by setting C, resetting N and H, and preserving Z.
    /// </summary>
    private static void SetCarryFlag(Cpu cpu)
    {
        cpu.Registers.SetFlag(CpuFlag.Carry, isSet: true);
        ResetSubtractAndHalfCarry(cpu);
    }

    /// <summary>
    /// Executes CCF by toggling C, resetting N and H, and preserving Z.
    /// </summary>
    private static void ComplementCarryFlag(Cpu cpu)
    {
        bool carry = cpu.Registers.IsFlagSet(CpuFlag.Carry);

        cpu.Registers.SetFlag(CpuFlag.Carry, !carry);
        ResetSubtractAndHalfCarry(cpu);
    }

    /// <summary>
    /// Shared SCF/CCF flag effect.
    /// </summary>
    private static void ResetSubtractAndHalfCarry(Cpu cpu)
    {
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
    }
}
