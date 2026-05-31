namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 stack instructions.
/// </summary>
internal static class StackInstructions
{
    private const byte PopRegisterPairStartOpcode = 0xC1;
    private const byte PopRegisterPairEndOpcode = 0xF1;
    private const byte PushRegisterPairStartOpcode = 0xC5;
    private const byte PushRegisterPairEndOpcode = 0xF5;

    private const int StackRegisterPairOpcodeStep = 0x10;

    private const byte StackRegisterPairMask = 0x03;
    private const int StackRegisterPairShift = 4;

    /// <summary>
    /// Executes one stack operation after the r16stk operand has been decoded.
    /// </summary>
    private delegate void StackRegisterPairExecutor(Cpu cpu, StackRegisterPair registerPair);

    /// <summary>
    /// Maps implemented stack instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapStackRegisterPair(
            builder,
            PopRegisterPairStartOpcode,
            PopRegisterPairEndOpcode,
            PopRegisterPair
        );
        MapStackRegisterPair(
            builder,
            PushRegisterPairStartOpcode,
            PushRegisterPairEndOpcode,
            PushRegisterPair
        );
    }

    /// <summary>
    /// Pops a 16-bit stack value into the selected r16stk register pair.
    /// </summary>
    private static void PopRegisterPair(Cpu cpu, StackRegisterPair registerPair)
    {
        cpu.Registers.SetStackRegisterPair(registerPair, cpu.PopWord());
    }

    /// <summary>
    /// Pushes the selected r16stk register pair value onto the stack.
    /// </summary>
    private static void PushRegisterPair(Cpu cpu, StackRegisterPair registerPair)
    {
        cpu.IdleCycle();
        cpu.PushWord(cpu.Registers.GetStackRegisterPair(registerPair));
    }

    /// <summary>
    /// Maps an inclusive r16stk opcode range whose operand is encoded in bits 5-4.
    /// </summary>
    private static void MapStackRegisterPair(
        OpcodeTableBuilder builder,
        byte startOpcode,
        byte endOpcode,
        StackRegisterPairExecutor execute
    )
    {
        for (int opcode = startOpcode; opcode <= endOpcode; opcode += StackRegisterPairOpcodeStep)
        {
            byte opcodeByte = (byte)opcode;
            StackRegisterPair registerPair = DecodeStackRegisterPair(opcodeByte);
            builder.MapNoOperand(opcodeByte, cpu => execute(cpu, registerPair));
        }
    }

    /// <summary>
    /// Decodes the r16stk operand stored in opcode bits 5-4.
    /// </summary>
    private static StackRegisterPair DecodeStackRegisterPair(byte opcode) =>
        (StackRegisterPair)((opcode >> StackRegisterPairShift) & StackRegisterPairMask);
}
