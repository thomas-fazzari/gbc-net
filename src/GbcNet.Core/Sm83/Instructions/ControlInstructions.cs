using System.Globalization;

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 control and no-operation instructions.
/// </summary>
internal static class ControlInstructions
{
    private const byte NopOpcode = 0x00;
    private const byte CbPrefixOpcode = 0xCB;
    private const byte HaltOpcode = 0x76;
    private const byte DisableInterruptsOpcode = 0xF3;
    private const byte EnableInterruptsOpcode = 0xFB;

    private const byte NoOperandByteLength = 1;
    private const byte PrefixedInstructionByteLength = 2;

    /// <summary>
    /// Maps implemented control instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(NopOpcode, NoOperandByteLength, static (_, _, _) => { });
        builder.Map(CbPrefixOpcode, PrefixedInstructionByteLength, ExecuteCbPrefix);
        builder.Map(HaltOpcode, NoOperandByteLength, static (cpu, _, _) => cpu.Halt());
        builder.Map(
            DisableInterruptsOpcode,
            NoOperandByteLength,
            static (cpu, _, _) => cpu.DisableInterrupts()
        );
        builder.Map(
            EnableInterruptsOpcode,
            NoOperandByteLength,
            static (cpu, _, _) => cpu.EnableInterruptsAfterNextInstruction()
        );
    }

    /// <summary>
    /// Executes a CB-prefixed opcode from the separate prefixed instruction table.
    /// </summary>
    private static void ExecuteCbPrefix(Cpu cpu, byte prefixedOpcode, byte _)
    {
        Instruction instruction =
            CbInstructionSet.Find(prefixedOpcode)
            ?? throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CB opcode 0x{0:X2} is not supported yet.",
                    prefixedOpcode
                )
            );

        instruction.Execute(cpu, 0, 0);
    }
}
