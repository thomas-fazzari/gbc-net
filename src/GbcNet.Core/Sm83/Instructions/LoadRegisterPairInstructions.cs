namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 load instructions that use r16 or r16mem operands.
/// </summary>
internal static class LoadRegisterPairInstructions
{
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
    private const byte LoadHlFromStackPointerPlusImmediate8Opcode = 0xF8;
    private const byte LoadStackPointerFromHlOpcode = 0xF9;

    private const byte NoOperandByteLength = 1;
    private const byte Immediate8ByteLength = 2;
    private const byte Immediate16ByteLength = 3;

    private const int LoadHlFromStackPointerPlusImmediate8MachineCycles = 3;
    private const int LoadRegisterPairAddressMachineCycles = 2;
    private const int LoadRegisterPairImmediate16MachineCycles = 3;
    private const int LoadImmediate16AddressFromStackPointerMachineCycles = 5;
    private const int LoadStackPointerFromHlMachineCycles = 2;

    /// <summary>
    /// Maps implemented r16 and r16mem load instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapLoadRegisterPairImmediate16(builder, LoadBcImmediate16Opcode, RegisterPair.BC);
        MapWriteAccumulatorToRegisterPairAddress(
            builder,
            LoadAddressBcFromAOpcode,
            RegisterPair.BC
        );
        builder.Map(
            LoadAddressImmediate16FromStackPointerOpcode,
            Immediate16ByteLength,
            ExecuteLoadAddressImmediate16FromStackPointer
        );
        MapReadAccumulatorFromRegisterPairAddress(
            builder,
            LoadAFromAddressBcOpcode,
            RegisterPair.BC
        );

        MapLoadRegisterPairImmediate16(builder, LoadDeImmediate16Opcode, RegisterPair.DE);
        MapWriteAccumulatorToRegisterPairAddress(
            builder,
            LoadAddressDeFromAOpcode,
            RegisterPair.DE
        );
        MapReadAccumulatorFromRegisterPairAddress(
            builder,
            LoadAFromAddressDeOpcode,
            RegisterPair.DE
        );

        MapLoadRegisterPairImmediate16(builder, LoadHlImmediate16Opcode, RegisterPair.HL);
        builder.Map(
            LoadAddressHlIncrementFromAOpcode,
            NoOperandByteLength,
            ExecuteLoadAddressHlIncrementFromA
        );
        builder.Map(
            LoadAFromAddressHlIncrementOpcode,
            NoOperandByteLength,
            ExecuteLoadAFromAddressHlIncrement
        );

        MapLoadRegisterPairImmediate16(builder, LoadSpImmediate16Opcode, RegisterPair.SP);
        builder.Map(
            LoadAddressHlDecrementFromAOpcode,
            NoOperandByteLength,
            ExecuteLoadAddressHlDecrementFromA
        );
        builder.Map(
            LoadAFromAddressHlDecrementOpcode,
            NoOperandByteLength,
            ExecuteLoadAFromAddressHlDecrement
        );
        builder.Map(
            LoadHlFromStackPointerPlusImmediate8Opcode,
            Immediate8ByteLength,
            ExecuteLoadHlFromStackPointerPlusImmediate8
        );
        builder.Map(
            LoadStackPointerFromHlOpcode,
            NoOperandByteLength,
            ExecuteLoadStackPointerFromHl
        );
    }

    /// <summary>
    /// Maps an LD r16, imm16 instruction for the selected 16-bit register pair.
    /// </summary>
    private static void MapLoadRegisterPairImmediate16(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.Map(
            opcode,
            Immediate16ByteLength,
            (cpu, lowByte, highByte) =>
            {
                LoadRegisterPairImmediate16(cpu, registerPair, lowByte, highByte);
                return LoadRegisterPairImmediate16MachineCycles;
            }
        );
    }

    /// <summary>
    /// Maps an LD [r16], A instruction for BC- and DE-addressed memory writes.
    /// </summary>
    private static void MapWriteAccumulatorToRegisterPairAddress(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            (cpu, _, _) =>
            {
                WriteAccumulatorToRegisterPairAddress(cpu, registerPair);
                return LoadRegisterPairAddressMachineCycles;
            }
        );
    }

    /// <summary>
    /// Maps an LD A, [r16] instruction for BC- and DE-addressed memory reads.
    /// </summary>
    private static void MapReadAccumulatorFromRegisterPairAddress(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.Map(
            opcode,
            NoOperandByteLength,
            (cpu, _, _) =>
            {
                ReadAccumulatorFromRegisterPairAddress(cpu, registerPair);
                return LoadRegisterPairAddressMachineCycles;
            }
        );
    }

    /// <summary>
    /// Executes LD [imm16], SP by writing SP as a little-endian word.
    /// </summary>
    private static int ExecuteLoadAddressImmediate16FromStackPointer(
        Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        WriteLittleEndianWord(
            cpu,
            InstructionOperands.ReadImmediate16(lowByte, highByte),
            cpu.Registers.SP
        );
        return LoadImmediate16AddressFromStackPointerMachineCycles;
    }

    /// <summary>
    /// Executes LD [HL+], A by writing A, then incrementing HL.
    /// </summary>
    private static int ExecuteLoadAddressHlIncrementFromA(Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.WriteByte(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
        return LoadRegisterPairAddressMachineCycles;
    }

    /// <summary>
    /// Executes LD A, [HL+] by reading A, then incrementing HL.
    /// </summary>
    private static int ExecuteLoadAFromAddressHlIncrement(Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadByte(address);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
        return LoadRegisterPairAddressMachineCycles;
    }

    /// <summary>
    /// Executes LD [HL-], A by writing A, then decrementing HL.
    /// </summary>
    private static int ExecuteLoadAddressHlDecrementFromA(Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.WriteByte(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
        return LoadRegisterPairAddressMachineCycles;
    }

    /// <summary>
    /// Executes LD A, [HL-] by reading A, then decrementing HL.
    /// </summary>
    private static int ExecuteLoadAFromAddressHlDecrement(Cpu cpu, byte lowByte, byte highByte)
    {
        ushort address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadByte(address);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
        return LoadRegisterPairAddressMachineCycles;
    }

    /// <summary>
    /// Executes LD HL, SP+imm8 using the SP+signed-imm8 flag rules.
    /// </summary>
    private static int ExecuteLoadHlFromStackPointerPlusImmediate8(
        Cpu cpu,
        byte offset,
        byte highByte
    )
    {
        (ushort value, byte flags) = InstructionOperands.AddSignedImmediate8WithFlags(
            cpu.Registers.SP,
            offset
        );
        cpu.Registers.HL = value;
        cpu.Registers.F = flags;
        return LoadHlFromStackPointerPlusImmediate8MachineCycles;
    }

    /// <summary>
    /// Executes LD SP, HL by copying HL into the stack pointer without changing flags.
    /// </summary>
    private static int ExecuteLoadStackPointerFromHl(Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.SP = cpu.Registers.HL;
        return LoadStackPointerFromHlMachineCycles;
    }

    /// <summary>
    /// Loads an imm16 operand into the selected r16 register pair.
    /// </summary>
    private static void LoadRegisterPairImmediate16(
        Cpu cpu,
        RegisterPair registerPair,
        byte lowByte,
        byte highByte
    )
    {
        cpu.Registers.SetRegisterPair(
            registerPair,
            InstructionOperands.ReadImmediate16(lowByte, highByte)
        );
    }

    /// <summary>
    /// Writes A to the address stored in the selected r16 register pair.
    /// </summary>
    private static void WriteAccumulatorToRegisterPairAddress(Cpu cpu, RegisterPair registerPair)
    {
        cpu.WriteByte(cpu.Registers.GetRegisterPair(registerPair), cpu.Registers.A);
    }

    /// <summary>
    /// Loads A from the address stored in the selected r16 register pair.
    /// </summary>
    private static void ReadAccumulatorFromRegisterPairAddress(Cpu cpu, RegisterPair registerPair)
    {
        cpu.Registers.A = cpu.ReadByte(cpu.Registers.GetRegisterPair(registerPair));
    }

    /// <summary>
    /// Writes a 16-bit value as low byte, then high byte.
    /// </summary>
    private static void WriteLittleEndianWord(Cpu cpu, ushort address, ushort value)
    {
        cpu.WriteByte(address, (byte)value);
        cpu.WriteByte(unchecked((ushort)(address + 1)), (byte)(value >> 8));
    }
}
