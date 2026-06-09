using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// DMG post-boot-ROM hand-off state.
/// </summary>
internal static class DmgPostBootState
{
    private const ushort DividerCounter = 0xABCC;
    private const byte Accumulator = 0x01;
    private const byte FlagsBase = (byte)CpuFlag.Zero;
    private const byte FlagsChecksumNonZero = (byte)(CpuFlag.HalfCarry | CpuFlag.Carry);
    private const ushort RegisterBc = 0x0013;
    private const ushort RegisterDe = 0x00D8;
    private const ushort RegisterHl = 0x014D;
    private const ushort AudioRegistersStart = 0xFF10;

    private static readonly PostBootHardwareRegisterState[] _registerStatesBeforeAudio =
    [
        new(AddressMap.JoypadRegister, 0xCF),
        new(AddressMap.SerialTransferDataRegister, 0x00),
        new(AddressMap.SerialTransferControlRegister, 0x7E),
        new(AddressMap.TimerCounterRegister, 0x00),
        new(AddressMap.TimerModuloRegister, 0x00),
        new(AddressMap.TimerControlRegister, 0x00),
        new(AddressMap.InterruptFlagRegister, 0x01),
    ];

    private static readonly PostBootHardwareRegisterState[] _registerStatesAfterAudio =
    [
        new(AddressMap.LcdControlRegister, 0x91),
        new(AddressMap.LcdStatusRegister, 0x85),
        new(AddressMap.ScrollYRegister, 0x00),
        new(AddressMap.ScrollXRegister, 0x00),
        new(AddressMap.LcdYCoordinateRegister, 0x00),
        new(AddressMap.LcdYCompareRegister, 0x00),
        new(AddressMap.DmaRegister, 0xFF),
        new(AddressMap.BackgroundPaletteRegister, 0xFC),
        new(AddressMap.WindowYRegister, 0x00),
        new(AddressMap.WindowXRegister, 0x00),
        new(AddressMap.InterruptEnableRegister, 0x00),
    ];

    /// <summary>
    /// DMG post-boot APU register values indexed from FF10 through FF26.
    /// </summary>
    private static ReadOnlySpan<byte> AudioRegisterStates =>
        [
            0x80,
            0xBF,
            0xF3,
            0xFF,
            0xBF,
            0xFF,
            0x3F,
            0x00,
            0xFF,
            0xBF,
            0x7F,
            0xFF,
            0x9F,
            0xFF,
            0xBF,
            0xFF,
            0xFF,
            0x00,
            0x00,
            0xBF,
            0x77,
            0xF3,
            0xF1,
        ];

    public static void Apply(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        PostBootState.SetCpuRegisters(cpu.Registers, CreateCpuRegisterState(cartridge));
        bus.Clock.SetCounter(DividerCounter);
        PostBootState.SetHardwareRegisterStates(bus, _registerStatesBeforeAudio);
        ApplyAudioRegisters(bus);
        PostBootState.SetHardwareRegisterStates(bus, _registerStatesAfterAudio);
    }

    private static PostBootCpuRegisterState CreateCpuRegisterState(Cartridge cartridge) =>
        new(
            Accumulator,
            cartridge.Header.HeaderChecksum is 0x00
                ? FlagsBase
                : (byte)(FlagsBase | FlagsChecksumNonZero),
            RegisterBc,
            RegisterDe,
            RegisterHl,
            AddressMap.CartridgeEntryPointStart,
            AddressMap.HighRamEnd
        );

    private static void ApplyAudioRegisters(MemoryBus bus)
    {
        ReadOnlySpan<byte> values = AudioRegisterStates;
        for (int offset = 0; offset < values.Length; offset++)
        {
            bus.SetHardwareRegisterState((ushort)(AudioRegistersStart + offset), values[offset]);
        }
    }
}
