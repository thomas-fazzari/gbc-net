using System.Globalization;

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 control and no-operation instructions.
/// </summary>
internal static class ControlInstructions
{
    private const byte NopOpcode = 0x00;
    private const byte StopOpcode = 0x10;
    private const byte CbPrefixOpcode = 0xCB;
    private const byte HaltOpcode = 0x76;
    private const byte DisableInterruptsOpcode = 0xF3;
    private const byte EnableInterruptsOpcode = 0xFB;

    /// <summary>
    /// Maps implemented control instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.MapNoOperand(NopOpcode, static _ => { });
        builder.MapImmediate8(StopOpcode, static (cpu, _) => cpu.Stop());
        builder.MapPrefixed(CbPrefixOpcode, ExecuteCbPrefix);
        builder.MapNoOperand(HaltOpcode, static cpu => cpu.Halt());
        builder.MapNoOperand(DisableInterruptsOpcode, static cpu => cpu.DisableInterrupts());
        builder.MapNoOperand(
            EnableInterruptsOpcode,
            static cpu => cpu.EnableInterruptsAfterNextInstruction()
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
