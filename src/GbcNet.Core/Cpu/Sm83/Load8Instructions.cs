namespace GbcNet.Core.Cpu.Sm83;

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

    private const byte Immediate8ByteLength = 2;

    private const int LoadAddressHlImmediate8MachineCycles = 3;
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
            LoadAddressHlImmediate8MachineCycles,
            static (cpu, value, _) => cpu.WriteByte(cpu.Registers.HL, value)
        );
        MapLoadRegisterImmediate8(builder, LoadAImmediate8Opcode, Register8.A);
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
            LoadRegisterImmediate8MachineCycles,
            (cpu, value, _) => cpu.Registers.SetRegister(register, value)
        );
    }
}
