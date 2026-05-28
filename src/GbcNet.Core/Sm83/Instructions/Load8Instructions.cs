namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 8-bit load instructions.
/// </summary>
internal static class Load8Instructions
{
    private const byte LoadBImmediate8Opcode = 0x06;
    private const byte LoadCImmediate8Opcode = 0x0E;
    private const byte LoadDImmediate8Opcode = 0x16;
    private const byte LoadEImmediate8Opcode = 0x1E;
    private const byte LoadHImmediate8Opcode = 0x26;
    private const byte LoadLImmediate8Opcode = 0x2E;
    private const byte LoadAddressHlImmediate8Opcode = 0x36;
    private const byte LoadAImmediate8Opcode = 0x3E;
    private const byte LoadRegisterOperandStartOpcode = 0x40;
    private const byte HaltOpcode = 0x76;
    private const byte LoadRegisterOperandEndOpcode = 0x7F;
    private const byte LoadHighAddressImmediate8FromAccumulatorOpcode = 0xE0;
    private const byte LoadHighAddressCFromAccumulatorOpcode = 0xE2;
    private const byte LoadAddressImmediate16FromAccumulatorOpcode = 0xEA;
    private const byte LoadAccumulatorFromHighAddressImmediate8Opcode = 0xF0;
    private const byte LoadAccumulatorFromHighAddressCOpcode = 0xF2;
    private const byte LoadAccumulatorFromAddressImmediate16Opcode = 0xFA;

    private const byte NoOperandByteLength = 1;
    private const byte Immediate8ByteLength = 2;
    private const byte Immediate16ByteLength = 3;

    /// <summary>
    /// Base address for SM83 high-memory load encodings, addressed as FF00+imm8 or FF00+C.
    /// </summary>
    private const ushort HighMemoryBaseAddress = 0xFF00;

    /// <summary>
    /// Maps implemented 8-bit load instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapLoadRegisterImmediate8(builder, LoadBImmediate8Opcode, Register8.B);
        MapLoadRegisterImmediate8(builder, LoadCImmediate8Opcode, Register8.C);
        MapLoadRegisterImmediate8(builder, LoadDImmediate8Opcode, Register8.D);
        MapLoadRegisterImmediate8(builder, LoadEImmediate8Opcode, Register8.E);
        MapLoadRegisterImmediate8(builder, LoadHImmediate8Opcode, Register8.H);
        MapLoadRegisterImmediate8(builder, LoadLImmediate8Opcode, Register8.L);
        builder.Map(
            LoadAddressHlImmediate8Opcode,
            Immediate8ByteLength,
            static (cpu, value, _) => cpu.WriteBus(cpu.Registers.HL, value)
        );
        MapLoadRegisterImmediate8(builder, LoadAImmediate8Opcode, Register8.A);
        MapLoadRegisterOperand(builder);
        builder.Map(
            LoadHighAddressImmediate8FromAccumulatorOpcode,
            Immediate8ByteLength,
            ExecuteLoadHighAddressImmediate8FromAccumulator
        );
        builder.Map(
            LoadHighAddressCFromAccumulatorOpcode,
            NoOperandByteLength,
            ExecuteLoadHighAddressCFromAccumulator
        );
        builder.Map(
            LoadAddressImmediate16FromAccumulatorOpcode,
            Immediate16ByteLength,
            ExecuteLoadAddressImmediate16FromAccumulator
        );
        builder.Map(
            LoadAccumulatorFromHighAddressImmediate8Opcode,
            Immediate8ByteLength,
            ExecuteLoadAccumulatorFromHighAddressImmediate8
        );
        builder.Map(
            LoadAccumulatorFromHighAddressCOpcode,
            NoOperandByteLength,
            ExecuteLoadAccumulatorFromHighAddressC
        );
        builder.Map(
            LoadAccumulatorFromAddressImmediate16Opcode,
            Immediate16ByteLength,
            ExecuteLoadAccumulatorFromAddressImmediate16
        );
    }

    /// <summary>
    /// Maps an LD r8, imm8 instruction, which loads the following byte without changing flags.
    /// </summary>
    private static void MapLoadRegisterImmediate8(
        OpcodeTableBuilder builder,
        byte opcode,
        Register8 register
    )
    {
        builder.Map(
            opcode,
            Immediate8ByteLength,
            (cpu, value, _) => cpu.Registers.SetRegister(register, value)
        );
    }

    /// <summary>
    /// Maps the LD r8, r8 block, excluding opcode 0x76 because it encodes HALT.
    /// </summary>
    private static void MapLoadRegisterOperand(OpcodeTableBuilder builder)
    {
        for (
            int opcode = LoadRegisterOperandStartOpcode;
            opcode <= LoadRegisterOperandEndOpcode;
            opcode++
        )
        {
            if (opcode is HaltOpcode)
            {
                continue;
            }

            byte opcodeByte = (byte)opcode;
            Register8Operand destination = Register8Operands.DecodeDestination(opcodeByte);
            Register8Operand source = Register8Operands.DecodeSource(opcodeByte);
            builder.Map(
                opcodeByte,
                NoOperandByteLength,
                (cpu, _, _) => LoadRegisterOperand(cpu, destination, source)
            );
        }
    }

    /// <summary>
    /// Executes LD r8, r8 without changing flags.
    /// </summary>
    private static void LoadRegisterOperand(
        Cpu cpu,
        Register8Operand destination,
        Register8Operand source
    )
    {
        byte value = Register8Operands.Read(cpu, source);
        Register8Operands.Write(cpu, destination, value);
    }

    /// <summary>
    /// Executes LDH [imm8], A by writing A to FF00+imm8.
    /// </summary>
    private static void ExecuteLoadHighAddressImmediate8FromAccumulator(
        Cpu cpu,
        byte offset,
        byte highByte
    )
    {
        cpu.WriteBus(GetHighMemoryAddress(offset), cpu.Registers.A);
    }

    /// <summary>
    /// Executes LDH [C], A by writing A to FF00+C.
    /// </summary>
    private static void ExecuteLoadHighAddressCFromAccumulator(Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.WriteBus(GetHighMemoryAddress(cpu.Registers.C), cpu.Registers.A);
    }

    /// <summary>
    /// Executes LD [imm16], A by writing A to the immediate address.
    /// </summary>
    private static void ExecuteLoadAddressImmediate16FromAccumulator(
        Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        cpu.WriteBus(InstructionOperands.ReadImmediate16(lowByte, highByte), cpu.Registers.A);
    }

    /// <summary>
    /// Executes LDH A, [imm8] by reading A from FF00+imm8.
    /// </summary>
    private static void ExecuteLoadAccumulatorFromHighAddressImmediate8(
        Cpu cpu,
        byte offset,
        byte highByte
    )
    {
        cpu.Registers.A = cpu.ReadBus(GetHighMemoryAddress(offset));
    }

    /// <summary>
    /// Executes LDH A, [C] by reading A from FF00+C.
    /// </summary>
    private static void ExecuteLoadAccumulatorFromHighAddressC(Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.A = cpu.ReadBus(GetHighMemoryAddress(cpu.Registers.C));
    }

    /// <summary>
    /// Executes LD A, [imm16] by reading A from the immediate address.
    /// </summary>
    private static void ExecuteLoadAccumulatorFromAddressImmediate16(
        Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        cpu.Registers.A = cpu.ReadBus(InstructionOperands.ReadImmediate16(lowByte, highByte));
    }

    /// <summary>
    /// Builds the high-memory address used by LDH-style FF00+offset encodings.
    /// </summary>
    private static ushort GetHighMemoryAddress(byte offset) =>
        (ushort)(HighMemoryBaseAddress + offset);
}
