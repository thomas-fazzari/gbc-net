using System.Globalization;

namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 control and no-operation instructions.
/// </summary>
internal static class ControlInstructions
{
    private const byte NopOpcode = 0x00;
    private const byte CbPrefixOpcode = 0xCB;
    private const byte DisableInterruptsOpcode = 0xF3;
    private const byte EnableInterruptsOpcode = 0xFB;

    private const byte NoOperandByteLength = 1;
    private const byte PrefixedInstructionByteLength = 2;

    private const int NopMachineCycles = 1;
    private const int InterruptMasterEnableMachineCycles = 1;

    /// <summary>
    /// Maps implemented control instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(NopOpcode, NoOperandByteLength, static (_, _, _) => NopMachineCycles);
        builder.Map(CbPrefixOpcode, PrefixedInstructionByteLength, ExecuteCbPrefix);
        builder.Map(
            DisableInterruptsOpcode,
            NoOperandByteLength,
            static (cpu, _, _) =>
            {
                cpu.DisableInterrupts();
                return InterruptMasterEnableMachineCycles;
            }
        );
        builder.Map(
            EnableInterruptsOpcode,
            NoOperandByteLength,
            static (cpu, _, _) =>
            {
                cpu.EnableInterruptsAfterNextInstruction();
                return InterruptMasterEnableMachineCycles;
            }
        );
    }

    /// <summary>
    /// Executes a CB-prefixed opcode from the separate prefixed instruction table.
    /// </summary>
    private static int ExecuteCbPrefix(Cpu cpu, byte prefixedOpcode, byte _)
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

        return instruction.Execute(cpu, 0, 0);
    }
}
