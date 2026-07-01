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

    /// <summary>
    /// Base address for SM83 high-memory load encodings, addressed as FF00+imm8 or FF00+C.
    /// </summary>
    private const ushort HighMemoryBaseAddress = 0xFF00;

    public static void Map(OpcodeTableBuilder builder)
    {
        MapLoadRegisterImmediate8(builder, LoadBImmediate8Opcode, Register8.B);
        MapLoadRegisterImmediate8(builder, LoadCImmediate8Opcode, Register8.C);
        MapLoadRegisterImmediate8(builder, LoadDImmediate8Opcode, Register8.D);
        MapLoadRegisterImmediate8(builder, LoadEImmediate8Opcode, Register8.E);
        MapLoadRegisterImmediate8(builder, LoadHImmediate8Opcode, Register8.H);
        MapLoadRegisterImmediate8(builder, LoadLImmediate8Opcode, Register8.L);
        builder.MapImmediate8(
            LoadAddressHlImmediate8Opcode,
            static (cpu, value) => cpu.WriteBus(cpu.Registers.HL, value)
        );
        MapLoadRegisterImmediate8(builder, LoadAImmediate8Opcode, Register8.A);
        MapLoadRegisterOperand(builder);
        builder.MapImmediate8(
            LoadHighAddressImmediate8FromAccumulatorOpcode,
            ExecuteLoadHighAddressImmediate8FromAccumulator
        );
        builder.MapNoOperand(
            LoadHighAddressCFromAccumulatorOpcode,
            ExecuteLoadHighAddressCFromAccumulator
        );
        builder.MapImmediate16(
            LoadAddressImmediate16FromAccumulatorOpcode,
            ExecuteLoadAddressImmediate16FromAccumulator
        );
        builder.MapImmediate8(
            LoadAccumulatorFromHighAddressImmediate8Opcode,
            ExecuteLoadAccumulatorFromHighAddressImmediate8
        );
        builder.MapNoOperand(
            LoadAccumulatorFromHighAddressCOpcode,
            ExecuteLoadAccumulatorFromHighAddressC
        );
        builder.MapImmediate16(
            LoadAccumulatorFromAddressImmediate16Opcode,
            ExecuteLoadAccumulatorFromAddressImmediate16
        );
    }

    private static void MapLoadRegisterImmediate8(
        OpcodeTableBuilder builder,
        byte opcode,
        Register8 register
    )
    {
        builder.MapImmediate8(opcode, (cpu, value) => cpu.Registers.SetRegister(register, value));
    }

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

            var opcodeByte = (byte)opcode;
            var destination = Register8Operands.DecodeDestination(opcodeByte);
            var source = Register8Operands.DecodeSource(opcodeByte);
            builder.MapNoOperand(opcodeByte, cpu => LoadRegisterOperand(cpu, destination, source));
        }
    }

    private static void LoadRegisterOperand(
        Cpu cpu,
        Register8Operand destination,
        Register8Operand source
    )
    {
        var value = Register8Operands.Read(cpu, source);
        Register8Operands.Write(cpu, destination, value);
    }

    private static void ExecuteLoadHighAddressImmediate8FromAccumulator(Cpu cpu, byte offset)
    {
        cpu.WriteBus(GetHighMemoryAddress(offset), cpu.Registers.A);
    }

    private static void ExecuteLoadHighAddressCFromAccumulator(Cpu cpu)
    {
        cpu.WriteBus(GetHighMemoryAddress(cpu.Registers.C), cpu.Registers.A);
    }

    private static void ExecuteLoadAddressImmediate16FromAccumulator(
        Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        cpu.WriteBus(InstructionOperands.ReadImmediate16(lowByte, highByte), cpu.Registers.A);
    }

    private static void ExecuteLoadAccumulatorFromHighAddressImmediate8(Cpu cpu, byte offset)
    {
        cpu.Registers.A = cpu.ReadBus(GetHighMemoryAddress(offset));
    }

    private static void ExecuteLoadAccumulatorFromHighAddressC(Cpu cpu)
    {
        cpu.Registers.A = cpu.ReadBus(GetHighMemoryAddress(cpu.Registers.C));
    }

    private static void ExecuteLoadAccumulatorFromAddressImmediate16(
        Cpu cpu,
        byte lowByte,
        byte highByte
    )
    {
        cpu.Registers.A = cpu.ReadBus(InstructionOperands.ReadImmediate16(lowByte, highByte));
    }

    private static ushort GetHighMemoryAddress(byte offset) =>
        (ushort)(HighMemoryBaseAddress + offset);
}
