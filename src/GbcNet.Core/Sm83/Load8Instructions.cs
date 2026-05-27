namespace GbcNet.Core.Sm83;

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

    private const byte NoOperandByteLength = 1;
    private const byte Immediate8ByteLength = 2;

    /// <summary>
    /// Mask for one r8 operand after shifting it to bits 2-0.
    /// </summary>
    private const byte RegisterOperandMask = 0x07;

    /// <summary>
    /// Right shift for the destination r8 operand encoded in opcode bits 5-3.
    /// </summary>
    private const int DestinationRegisterOperandShift = 3;

    private const int LoadAddressHlRegisterOperandMachineCycles = 2;
    private const int LoadAddressHlImmediate8MachineCycles = 3;
    private const int LoadRegisterOperandMachineCycles = 1;
    private const int LoadRegisterImmediate8MachineCycles = 2;

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
            static (cpu, value, _) =>
            {
                cpu.WriteByte(cpu.Registers.HL, value);
                return LoadAddressHlImmediate8MachineCycles;
            }
        );
        MapLoadRegisterImmediate8(builder, LoadAImmediate8Opcode, Register8.A);
        MapLoadRegisterOperand(builder);
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
            (cpu, value, _) =>
            {
                cpu.Registers.SetRegister(register, value);
                return LoadRegisterImmediate8MachineCycles;
            }
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

            var destination = (Register8Operand)(
                (opcode >> DestinationRegisterOperandShift) & RegisterOperandMask
            );
            var source = (Register8Operand)(opcode & RegisterOperandMask);
            builder.Map(
                (byte)opcode,
                NoOperandByteLength,
                (cpu, _, _) => LoadRegisterOperand(cpu, destination, source)
            );
        }
    }

    /// <summary>
    /// Executes LD r8, r8 without changing flags.
    /// </summary>
    private static int LoadRegisterOperand(
        Cpu cpu,
        Register8Operand destination,
        Register8Operand source
    )
    {
        byte value = ReadRegisterOperand(cpu, source);
        WriteRegisterOperand(cpu, destination, value);

        return destination is Register8Operand.AddressHl || source is Register8Operand.AddressHl
            ? LoadAddressHlRegisterOperandMachineCycles
            : LoadRegisterOperandMachineCycles;
    }

    /// <summary>
    /// Reads either a CPU register or the byte at HL.
    /// </summary>
    private static byte ReadRegisterOperand(Cpu cpu, Register8Operand operand) =>
        operand is Register8Operand.AddressHl
            ? cpu.ReadByte(cpu.Registers.HL)
            : cpu.Registers.GetRegister((Register8)operand);

    /// <summary>
    /// Writes either a CPU register or the byte at HL.
    /// </summary>
    private static void WriteRegisterOperand(Cpu cpu, Register8Operand operand, byte value)
    {
        if (operand is Register8Operand.AddressHl)
        {
            cpu.WriteByte(cpu.Registers.HL, value);
            return;
        }

        cpu.Registers.SetRegister((Register8)operand, value);
    }
}
