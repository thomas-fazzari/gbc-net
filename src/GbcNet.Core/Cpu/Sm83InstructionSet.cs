namespace GbcNet.Core.Cpu;

/// <summary>
/// SM83 opcode table entries.
/// </summary>
internal static class Sm83InstructionSet
{
    private const int OpcodeCount = 256;

    // Implemented primary opcodes.
    private const byte NopOpcode = 0x00;
    private const byte LoadBcImmediate16Opcode = 0x01;
    private const byte LoadDeImmediate16Opcode = 0x11;
    private const byte LoadHlImmediate16Opcode = 0x21;
    private const byte LoadSpImmediate16Opcode = 0x31;

    // Total instruction lengths in bytes, including the opcode byte.
    private const byte NopByteLength = 1;
    private const byte Immediate16ByteLength = 3;

    private const int NopMachineCycles = 1;
    private const int LoadRegisterPairImmediate16MachineCycles = 3;

    private static readonly Sm83Instruction?[] _instructions = CreateInstructions();

    /// <summary>
    /// Gets the opcode entry, or null when that opcode is not implemented yet.
    /// </summary>
    public static Sm83Instruction? Find(byte opcode) => _instructions[opcode];

    #region Opcode Table

    private static Sm83Instruction?[] CreateInstructions()
    {
        var instructions = new Sm83Instruction?[OpcodeCount];

        Map(instructions, NopOpcode, NopByteLength, NopMachineCycles, ExecuteNop);
        Map(
            instructions,
            LoadBcImmediate16Opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            ExecuteLoadBcImmediate16
        );
        Map(
            instructions,
            LoadDeImmediate16Opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            ExecuteLoadDeImmediate16
        );
        Map(
            instructions,
            LoadHlImmediate16Opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            ExecuteLoadHlImmediate16
        );
        Map(
            instructions,
            LoadSpImmediate16Opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            ExecuteLoadSpImmediate16
        );

        return instructions;
    }

    #endregion Opcode Table

    private static void Map(
        Sm83Instruction?[] instructions,
        byte opcode,
        byte byteLength,
        int machineCycles,
        Action<Sm83Cpu, byte, byte> execute
    )
    {
        instructions[opcode] = new Sm83Instruction(byteLength, machineCycles, execute);
    }

    #region Handlers

    private static void ExecuteNop(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        // Intentionally changes no CPU state beyond the PC advance done while fetching.
    }

    private static void ExecuteLoadBcImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.BC = ReadImmediate16(lowByte, highByte);
    }

    private static void ExecuteLoadDeImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.DE = ReadImmediate16(lowByte, highByte);
    }

    private static void ExecuteLoadHlImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.HL = ReadImmediate16(lowByte, highByte);
    }

    private static void ExecuteLoadSpImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.SP = ReadImmediate16(lowByte, highByte);
    }

    #endregion Handlers

    /// <summary>
    /// Combines an imm16 operand stored by the SM83 as low byte, then high byte.
    /// </summary>
    private static ushort ReadImmediate16(byte lowByte, byte highByte) =>
        (ushort)((highByte << 8) | lowByte);
}
