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
    private const byte LoadAddressBcFromAOpcode = 0x02;
    private const byte LoadAddressImmediate16FromStackPointerOpcode = 0x08;
    private const byte LoadAFromAddressBcOpcode = 0x0A;
    private const byte LoadDeImmediate16Opcode = 0x11;
    private const byte LoadAddressDeFromAOpcode = 0x12;
    private const byte LoadAFromAddressDeOpcode = 0x1A;
    private const byte LoadHlImmediate16Opcode = 0x21;
    private const byte LoadAddressHlIncrementFromAOpcode = 0x22;
    private const byte LoadAFromAddressHlIncrementOpcode = 0x2A;
    private const byte LoadSpImmediate16Opcode = 0x31;
    private const byte LoadAddressHlDecrementFromAOpcode = 0x32;
    private const byte LoadAFromAddressHlDecrementOpcode = 0x3A;

    // Total instruction lengths in bytes, including the opcode byte.
    private const byte NoOperandByteLength = 1;
    private const byte Immediate16ByteLength = 3;

    private const int NopMachineCycles = 1;
    private const int LoadRegisterPairAddressMachineCycles = 2;
    private const int LoadRegisterPairImmediate16MachineCycles = 3;
    private const int LoadImmediate16AddressFromStackPointerMachineCycles = 5;

    private static readonly Sm83Instruction?[] _instructions = CreateInstructions();

    /// <summary>
    /// Gets the opcode entry, or null when that opcode is not implemented yet.
    /// </summary>
    public static Sm83Instruction? Find(byte opcode) => _instructions[opcode];

    #region Opcode Table

    private static Sm83Instruction?[] CreateInstructions()
    {
        var instructions = new Sm83Instruction?[OpcodeCount];

        Map(instructions, NopOpcode, NoOperandByteLength, NopMachineCycles, ExecuteNop);
        Map(
            instructions,
            LoadBcImmediate16Opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            ExecuteLoadBcImmediate16
        );
        Map(
            instructions,
            LoadAddressBcFromAOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAddressBcFromA
        );
        Map(
            instructions,
            LoadAddressImmediate16FromStackPointerOpcode,
            Immediate16ByteLength,
            LoadImmediate16AddressFromStackPointerMachineCycles,
            ExecuteLoadAddressImmediate16FromStackPointer
        );
        Map(
            instructions,
            LoadAFromAddressBcOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAFromAddressBc
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
            LoadAddressDeFromAOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAddressDeFromA
        );
        Map(
            instructions,
            LoadAFromAddressDeOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAFromAddressDe
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
            LoadAddressHlIncrementFromAOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAddressHlIncrementFromA
        );
        Map(
            instructions,
            LoadAFromAddressHlIncrementOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAFromAddressHlIncrement
        );
        Map(
            instructions,
            LoadSpImmediate16Opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            ExecuteLoadSpImmediate16
        );
        Map(
            instructions,
            LoadAddressHlDecrementFromAOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAddressHlDecrementFromA
        );
        Map(
            instructions,
            LoadAFromAddressHlDecrementOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAFromAddressHlDecrement
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

    private static void ExecuteLoadAddressBcFromA(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.WriteByte(cpu.Registers.BC, cpu.Registers.A);
    }

    private static void ExecuteLoadAddressImmediate16FromStackPointer(
        Sm83Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        WriteLittleEndianWord(cpu, ReadImmediate16(lowByte, highByte), cpu.Registers.SP);
    }

    private static void ExecuteLoadAFromAddressBc(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.A = cpu.ReadByte(cpu.Registers.BC);
    }

    private static void ExecuteLoadDeImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.DE = ReadImmediate16(lowByte, highByte);
    }

    private static void ExecuteLoadAddressDeFromA(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.WriteByte(cpu.Registers.DE, cpu.Registers.A);
    }

    private static void ExecuteLoadAFromAddressDe(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.A = cpu.ReadByte(cpu.Registers.DE);
    }

    private static void ExecuteLoadHlImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.HL = ReadImmediate16(lowByte, highByte);
    }

    private static void ExecuteLoadAddressHlIncrementFromA(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.WriteByte(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
    }

    private static void ExecuteLoadAFromAddressHlIncrement(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadByte(address);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
    }

    private static void ExecuteLoadSpImmediate16(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.SP = ReadImmediate16(lowByte, highByte);
    }

    private static void ExecuteLoadAddressHlDecrementFromA(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.WriteByte(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
    }

    private static void ExecuteLoadAFromAddressHlDecrement(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadByte(address);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
    }

    #endregion Handlers

    /// <summary>
    /// Combines an imm16 operand stored by the SM83 as low byte, then high byte.
    /// </summary>
    private static ushort ReadImmediate16(byte lowByte, byte highByte) =>
        (ushort)((highByte << 8) | lowByte);

    /// <summary>
    /// Writes a 16-bit value as low byte, then high byte.
    /// </summary>
    private static void WriteLittleEndianWord(Sm83Cpu cpu, ushort address, ushort value)
    {
        cpu.WriteByte(address, (byte)value);
        cpu.WriteByte(unchecked((ushort)(address + 1)), (byte)(value >> 8));
    }
}
