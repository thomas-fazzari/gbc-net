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
    private const byte IncrementBcOpcode = 0x03;
    private const byte LoadAddressImmediate16FromStackPointerOpcode = 0x08;
    private const byte LoadAFromAddressBcOpcode = 0x0A;
    private const byte DecrementBcOpcode = 0x0B;
    private const byte LoadDeImmediate16Opcode = 0x11;
    private const byte LoadAddressDeFromAOpcode = 0x12;
    private const byte IncrementDeOpcode = 0x13;
    private const byte LoadAFromAddressDeOpcode = 0x1A;
    private const byte DecrementDeOpcode = 0x1B;
    private const byte LoadHlImmediate16Opcode = 0x21;
    private const byte LoadAddressHlIncrementFromAOpcode = 0x22;
    private const byte IncrementHlOpcode = 0x23;
    private const byte LoadAFromAddressHlIncrementOpcode = 0x2A;
    private const byte DecrementHlOpcode = 0x2B;
    private const byte LoadSpImmediate16Opcode = 0x31;
    private const byte LoadAddressHlDecrementFromAOpcode = 0x32;
    private const byte IncrementSpOpcode = 0x33;
    private const byte LoadAFromAddressHlDecrementOpcode = 0x3A;
    private const byte DecrementSpOpcode = 0x3B;

    // Total instruction lengths in bytes, including the opcode byte.
    private const byte NoOperandByteLength = 1;
    private const byte Immediate16ByteLength = 3;

    private const int NopMachineCycles = 1;
    private const int IncrementDecrementRegisterPairMachineCycles = 2;
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
        MapLoadRegisterPairImmediate16(instructions, LoadBcImmediate16Opcode, Sm83RegisterPair.BC);
        MapWriteAccumulatorToRegisterPairAddress(
            instructions,
            LoadAddressBcFromAOpcode,
            Sm83RegisterPair.BC
        );
        MapIncrementRegisterPair(instructions, IncrementBcOpcode, Sm83RegisterPair.BC);
        Map(
            instructions,
            LoadAddressImmediate16FromStackPointerOpcode,
            Immediate16ByteLength,
            LoadImmediate16AddressFromStackPointerMachineCycles,
            ExecuteLoadAddressImmediate16FromStackPointer
        );
        MapReadAccumulatorFromRegisterPairAddress(
            instructions,
            LoadAFromAddressBcOpcode,
            Sm83RegisterPair.BC
        );
        MapDecrementRegisterPair(instructions, DecrementBcOpcode, Sm83RegisterPair.BC);
        MapLoadRegisterPairImmediate16(instructions, LoadDeImmediate16Opcode, Sm83RegisterPair.DE);
        MapWriteAccumulatorToRegisterPairAddress(
            instructions,
            LoadAddressDeFromAOpcode,
            Sm83RegisterPair.DE
        );
        MapIncrementRegisterPair(instructions, IncrementDeOpcode, Sm83RegisterPair.DE);
        MapReadAccumulatorFromRegisterPairAddress(
            instructions,
            LoadAFromAddressDeOpcode,
            Sm83RegisterPair.DE
        );
        MapDecrementRegisterPair(instructions, DecrementDeOpcode, Sm83RegisterPair.DE);
        MapLoadRegisterPairImmediate16(instructions, LoadHlImmediate16Opcode, Sm83RegisterPair.HL);
        Map(
            instructions,
            LoadAddressHlIncrementFromAOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAddressHlIncrementFromA
        );
        MapIncrementRegisterPair(instructions, IncrementHlOpcode, Sm83RegisterPair.HL);
        Map(
            instructions,
            LoadAFromAddressHlIncrementOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAFromAddressHlIncrement
        );
        MapDecrementRegisterPair(instructions, DecrementHlOpcode, Sm83RegisterPair.HL);
        MapLoadRegisterPairImmediate16(instructions, LoadSpImmediate16Opcode, Sm83RegisterPair.SP);
        Map(
            instructions,
            LoadAddressHlDecrementFromAOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAddressHlDecrementFromA
        );
        MapIncrementRegisterPair(instructions, IncrementSpOpcode, Sm83RegisterPair.SP);
        Map(
            instructions,
            LoadAFromAddressHlDecrementOpcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            ExecuteLoadAFromAddressHlDecrement
        );
        MapDecrementRegisterPair(instructions, DecrementSpOpcode, Sm83RegisterPair.SP);

        return instructions;
    }

    #endregion Opcode Table

    /// <summary>
    /// Stores one implemented primary opcode in the instruction table.
    /// </summary>
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

    /// <summary>
    /// Maps an LD r16, imm16 instruction for the selected 16-bit register pair.
    /// </summary>
    private static void MapLoadRegisterPairImmediate16(
        Sm83Instruction?[] instructions,
        byte opcode,
        Sm83RegisterPair registerPair
    )
    {
        Map(
            instructions,
            opcode,
            Immediate16ByteLength,
            LoadRegisterPairImmediate16MachineCycles,
            (cpu, lowByte, highByte) =>
                LoadRegisterPairImmediate16(cpu, registerPair, lowByte, highByte)
        );
    }

    /// <summary>
    /// Maps an LD [r16], A instruction for BC- and DE-addressed memory writes.
    /// </summary>
    private static void MapWriteAccumulatorToRegisterPairAddress(
        Sm83Instruction?[] instructions,
        byte opcode,
        Sm83RegisterPair registerPair
    )
    {
        Map(
            instructions,
            opcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            (cpu, _, _) => WriteAccumulatorToRegisterPairAddress(cpu, registerPair)
        );
    }

    /// <summary>
    /// Maps an LD A, [r16] instruction for BC- and DE-addressed memory reads.
    /// </summary>
    private static void MapReadAccumulatorFromRegisterPairAddress(
        Sm83Instruction?[] instructions,
        byte opcode,
        Sm83RegisterPair registerPair
    )
    {
        Map(
            instructions,
            opcode,
            NoOperandByteLength,
            LoadRegisterPairAddressMachineCycles,
            (cpu, _, _) => ReadAccumulatorFromRegisterPairAddress(cpu, registerPair)
        );
    }

    /// <summary>
    /// Maps an INC r16 instruction, which wraps at 16 bits and leaves flags unchanged.
    /// </summary>
    private static void MapIncrementRegisterPair(
        Sm83Instruction?[] instructions,
        byte opcode,
        Sm83RegisterPair registerPair
    )
    {
        Map(
            instructions,
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
        Sm83Instruction?[] instructions,
        byte opcode,
        Sm83RegisterPair registerPair
    )
    {
        Map(
            instructions,
            opcode,
            NoOperandByteLength,
            IncrementDecrementRegisterPairMachineCycles,
            (cpu, _, _) => DecrementRegisterPair(cpu, registerPair)
        );
    }

    #region Handlers

    /// <summary>
    /// Executes NOP; instruction fetch already advanced PC.
    /// </summary>
    private static void ExecuteNop(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        // Intentionally changes no CPU state beyond the PC advance done while fetching.
    }

    /// <summary>
    /// Executes LD [imm16], SP by writing SP as a little-endian word.
    /// </summary>
    private static void ExecuteLoadAddressImmediate16FromStackPointer(
        Sm83Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        WriteLittleEndianWord(cpu, ReadImmediate16(lowByte, highByte), cpu.Registers.SP);
    }

    /// <summary>
    /// Executes LD [HL+], A by writing A, then incrementing HL.
    /// </summary>
    private static void ExecuteLoadAddressHlIncrementFromA(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.WriteByte(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
    }

    /// <summary>
    /// Executes LD A, [HL+] by reading A, then incrementing HL.
    /// </summary>
    private static void ExecuteLoadAFromAddressHlIncrement(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadByte(address);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
    }

    /// <summary>
    /// Executes LD [HL-], A by writing A, then decrementing HL.
    /// </summary>
    private static void ExecuteLoadAddressHlDecrementFromA(Sm83Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.WriteByte(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
    }

    /// <summary>
    /// Executes LD A, [HL-] by reading A, then decrementing HL.
    /// </summary>
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
    /// Loads an imm16 operand into the selected r16 register pair.
    /// </summary>
    private static void LoadRegisterPairImmediate16(
        Sm83Cpu cpu,
        Sm83RegisterPair registerPair,
        byte lowByte,
        byte highByte
    )
    {
        cpu.Registers.SetRegisterPair(registerPair, ReadImmediate16(lowByte, highByte));
    }

    /// <summary>
    /// Writes A to the address stored in the selected r16 register pair.
    /// </summary>
    private static void WriteAccumulatorToRegisterPairAddress(
        Sm83Cpu cpu,
        Sm83RegisterPair registerPair
    )
    {
        cpu.WriteByte(cpu.Registers.GetRegisterPair(registerPair), cpu.Registers.A);
    }

    /// <summary>
    /// Loads A from the address stored in the selected r16 register pair.
    /// </summary>
    private static void ReadAccumulatorFromRegisterPairAddress(
        Sm83Cpu cpu,
        Sm83RegisterPair registerPair
    )
    {
        cpu.Registers.A = cpu.ReadByte(cpu.Registers.GetRegisterPair(registerPair));
    }

    /// <summary>
    /// Increments the selected r16 register pair with 16-bit wraparound.
    /// </summary>
    private static void IncrementRegisterPair(Sm83Cpu cpu, Sm83RegisterPair registerPair)
    {
        ushort value = cpu.Registers.GetRegisterPair(registerPair);
        cpu.Registers.SetRegisterPair(registerPair, unchecked((ushort)(value + 1)));
    }

    /// <summary>
    /// Decrements the selected r16 register pair with 16-bit wraparound.
    /// </summary>
    private static void DecrementRegisterPair(Sm83Cpu cpu, Sm83RegisterPair registerPair)
    {
        ushort value = cpu.Registers.GetRegisterPair(registerPair);
        cpu.Registers.SetRegisterPair(registerPair, unchecked((ushort)(value - 1)));
    }

    /// <summary>
    /// Writes a 16-bit value as low byte, then high byte.
    /// </summary>
    private static void WriteLittleEndianWord(Sm83Cpu cpu, ushort address, ushort value)
    {
        cpu.WriteByte(address, (byte)value);
        cpu.WriteByte(unchecked((ushort)(address + 1)), (byte)(value >> 8));
    }
}
