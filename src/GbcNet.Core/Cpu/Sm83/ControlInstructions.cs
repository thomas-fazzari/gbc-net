namespace GbcNet.Core.Cpu.Sm83;

/// <summary>
/// SM83 control and no-operation instructions.
/// </summary>
internal static class ControlInstructions
{
    private const byte NopOpcode = 0x00;
    private const byte NoOperandByteLength = 1;

    private const int NopMachineCycles = 1;

    /// <summary>
    /// Maps implemented control instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(NopOpcode, NoOperandByteLength, NopMachineCycles, static (_, _, _) => { });
    }
}
