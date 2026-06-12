using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// CGB post-boot-ROM hand-off state for CGB and DMG compatibility modes.
/// </summary>
internal static class CgbPostBootState
{
    private const byte Accumulator = 0x11;
    private const byte Flags = (byte)CpuFlag.Zero;
    private const ushort CgbRegisterBc = 0x0000;
    private const ushort CgbRegisterDe = 0xFF56;
    private const ushort CgbRegisterHl = 0x000D;
    private const ushort DmgCompatibilityRegisterDe = 0x0008;
    private const ushort DmgCompatibilityDefaultRegisterHl = 0x007C;
    private const ushort DmgCompatibilityLogoRegisterHl = 0x991A;
    private const ushort AudioRegistersStart = 0xFF10;

    /// <summary>
    /// Retail CGB ABC/DE boot ROM hand-off divider phase at PC=$0100, validated against Mooneye boot_div-cgbABCDE.
    /// </summary>
    private const ushort RetailCgbAbcdeDividerCounter = 0x2678;

    private static readonly PostBootHardwareRegisterState[] _preAudioRegisters =
    [
        new(AddressMap.SerialTransferDataRegister, 0x00),
        new(AddressMap.SerialTransferControlRegister, 0x7E),
        new(AddressMap.TimerCounterRegister, 0x00),
        new(AddressMap.TimerModuloRegister, 0x00),
        new(AddressMap.TimerControlRegister, 0x00),
        new(AddressMap.InterruptFlagRegister, 0xE1),
    ];

    private static readonly PostBootHardwareRegisterState[] _commonPostAudioRegisters =
    [
        new(AddressMap.LcdControlRegister, 0x91),
        new(AddressMap.ScrollYRegister, 0x00),
        new(AddressMap.ScrollXRegister, 0x00),
        new(AddressMap.LcdYCompareRegister, 0x00),
        new(AddressMap.DmaRegister, 0x00),
        new(AddressMap.BackgroundPaletteRegister, 0xFC),
        new(AddressMap.WindowYRegister, 0x00),
        new(AddressMap.WindowXRegister, 0x00),
        new(AddressMap.InterruptEnableRegister, 0x00),
    ];

    private static readonly PostBootHardwareRegisterState[] _cgbModePostAudioRegisters =
    [
        new(AddressMap.Key1Register, 0x7E),
        new(AddressMap.ObjectPriorityModeRegister, 0xFE),
        new(AddressMap.VideoRamBankRegister, 0xFE),
        new(AddressMap.WorkRamBankRegister, 0xF8),
    ];

    private static readonly PostBootHardwareRegisterState[] _dmgCompatibilityPostAudioRegisters =
    [
        new(AddressMap.VideoRamBankRegister, 0xFE),
        new(AddressMap.BackgroundPaletteIndexRegister, 0xC8),
        new(AddressMap.ObjectPaletteIndexRegister, 0xD0),
    ];

    /// <summary>
    /// CGB post-boot APU register values indexed from FF10 through FF26.
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

    public static void Apply(
        CgbOperatingMode operatingMode,
        Cartridge cartridge,
        Cpu cpu,
        MemoryBus bus
    )
    {
        CgbCompatibilityPaletteSelection compatibilityPaletteSelection = default;

        switch (operatingMode)
        {
            case CgbOperatingMode.Cgb:
                PostBootState.SetCpuRegisters(cpu.Registers, CreateCgbModeCpuRegisterState());
                break;

            case CgbOperatingMode.DmgCompatibility:
                compatibilityPaletteSelection = CgbCompatibilityPaletteSelector.Select(cartridge);
                PostBootState.SetCpuRegisters(
                    cpu.Registers,
                    CreateDmgCompatibilityCpuRegisterState(compatibilityPaletteSelection)
                );
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operatingMode),
                    operatingMode,
                    "Unsupported CGB operating mode."
                );
        }

        PostBootState.SetHardwareRegisterStates(bus, _preAudioRegisters);
        ApplyAudioRegisters(bus);
        PostBootState.SetHardwareRegisterStates(bus, _commonPostAudioRegisters);
        bus.Clock.SetCounter(RetailCgbAbcdeDividerCounter);

        if (operatingMode is CgbOperatingMode.Cgb)
        {
            PostBootState.SetHardwareRegisterStates(bus, _cgbModePostAudioRegisters);
            bus.Ppu.SetBackgroundColorPaletteRamToWhite();
            return;
        }

        PostBootState.SetHardwareRegisterStates(bus, _dmgCompatibilityPostAudioRegisters);
        bus.Ppu.SetDmgCompatibilityColorPaletteRam(compatibilityPaletteSelection.Palettes);
    }

    private static PostBootCpuRegisterState CreateCgbModeCpuRegisterState() =>
        new(
            Accumulator,
            Flags,
            CgbRegisterBc,
            CgbRegisterDe,
            CgbRegisterHl,
            AddressMap.CartridgeEntryPointStart,
            AddressMap.HighRamEnd
        );

    private static PostBootCpuRegisterState CreateDmgCompatibilityCpuRegisterState(
        CgbCompatibilityPaletteSelection compatibilityPaletteSelection
    )
    {
        var registerHl = compatibilityPaletteSelection.UsesCompatibilityLogoTilemap
            ? DmgCompatibilityLogoRegisterHl
            : DmgCompatibilityDefaultRegisterHl;

        return new PostBootCpuRegisterState(
            Accumulator,
            Flags,
            (ushort)(compatibilityPaletteSelection.TitleChecksum << 8),
            DmgCompatibilityRegisterDe,
            registerHl,
            AddressMap.CartridgeEntryPointStart,
            AddressMap.HighRamEnd
        );
    }

    private static void ApplyAudioRegisters(MemoryBus bus)
    {
        var values = AudioRegisterStates;
        for (var offset = 0; offset < values.Length; offset++)
        {
            bus.SetHardwareRegisterState((ushort)(AudioRegistersStart + offset), values[offset]);
        }
    }
}
