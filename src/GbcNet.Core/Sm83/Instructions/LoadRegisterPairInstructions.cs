namespace GbcNet.Core.Sm83.Instructions;

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

    public static void Map(OpcodeTableBuilder builder)
    {
        MapLoadRegisterPairImmediate16(builder, LoadBcImmediate16Opcode, RegisterPair.BC);
        MapWriteAccumulatorToRegisterPairAddress(
            builder,
            LoadAddressBcFromAOpcode,
            RegisterPair.BC
        );
        builder.MapImmediate16(
            LoadAddressImmediate16FromStackPointerOpcode,
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
        builder.MapNoOperand(LoadAddressHlIncrementFromAOpcode, ExecuteLoadAddressHlIncrementFromA);
        builder.MapNoOperand(LoadAFromAddressHlIncrementOpcode, ExecuteLoadAFromAddressHlIncrement);

        MapLoadRegisterPairImmediate16(builder, LoadSpImmediate16Opcode, RegisterPair.SP);
        builder.MapNoOperand(LoadAddressHlDecrementFromAOpcode, ExecuteLoadAddressHlDecrementFromA);
        builder.MapNoOperand(LoadAFromAddressHlDecrementOpcode, ExecuteLoadAFromAddressHlDecrement);
        builder.MapImmediate8(
            LoadHlFromStackPointerPlusImmediate8Opcode,
            ExecuteLoadHlFromStackPointerPlusImmediate8
        );
        builder.MapNoOperand(LoadStackPointerFromHlOpcode, ExecuteLoadStackPointerFromHl);
    }

    private static void MapLoadRegisterPairImmediate16(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.MapImmediate16(
            opcode,
            (cpu, lowByte, highByte) =>
                LoadRegisterPairImmediate16(cpu, registerPair, lowByte, highByte)
        );
    }

    private static void MapWriteAccumulatorToRegisterPairAddress(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.MapNoOperand(
            opcode,
            cpu => WriteAccumulatorToRegisterPairAddress(cpu, registerPair)
        );
    }

    private static void MapReadAccumulatorFromRegisterPairAddress(
        OpcodeTableBuilder builder,
        byte opcode,
        RegisterPair registerPair
    )
    {
        builder.MapNoOperand(
            opcode,
            cpu => ReadAccumulatorFromRegisterPairAddress(cpu, registerPair)
        );
    }

    private static void ExecuteLoadAddressImmediate16FromStackPointer(
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
    }

    private static void ExecuteLoadAddressHlIncrementFromA(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        cpu.WriteBus(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
    }

    private static void ExecuteLoadAFromAddressHlIncrement(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadBus(address);
        cpu.Registers.HL = unchecked((ushort)(address + 1));
    }

    private static void ExecuteLoadAddressHlDecrementFromA(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        cpu.WriteBus(address, cpu.Registers.A);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
    }

    private static void ExecuteLoadAFromAddressHlDecrement(Cpu cpu)
    {
        var address = cpu.Registers.HL;
        cpu.Registers.A = cpu.ReadBus(address);
        cpu.Registers.HL = unchecked((ushort)(address - 1));
    }

    private static void ExecuteLoadHlFromStackPointerPlusImmediate8(Cpu cpu, byte offset)
    {
        var (value, flags) = InstructionOperands.AddSignedImmediate8WithFlags(
            cpu.Registers.SP,
            offset
        );
        cpu.Registers.HL = value;
        cpu.Registers.F = flags;
        cpu.IdleCycle();
    }

    private static void ExecuteLoadStackPointerFromHl(Cpu cpu)
    {
        cpu.Registers.SP = cpu.Registers.HL;
        cpu.IdleCycle();
    }

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

    private static void WriteAccumulatorToRegisterPairAddress(Cpu cpu, RegisterPair registerPair)
    {
        cpu.WriteBus(cpu.Registers.GetRegisterPair(registerPair), cpu.Registers.A);
    }

    private static void ReadAccumulatorFromRegisterPairAddress(Cpu cpu, RegisterPair registerPair)
    {
        cpu.Registers.A = cpu.ReadBus(cpu.Registers.GetRegisterPair(registerPair));
    }

    private static void WriteLittleEndianWord(Cpu cpu, ushort address, ushort value)
    {
        cpu.WriteBus(address, (byte)value);
        cpu.WriteBus(unchecked((ushort)(address + 1)), (byte)(value >> 8));
    }
}
