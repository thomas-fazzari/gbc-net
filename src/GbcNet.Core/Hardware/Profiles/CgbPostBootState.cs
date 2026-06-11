using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// CGB post-boot-ROM hand-off state for CGB and DMG compatibility modes.
/// </summary>
internal static class CgbPostBootState
{
    private const int TitleStartAddress = 0x0134;
    private const int TitleEndAddress = 0x0143;
    private const int NewLicenseeCodeStartAddress = 0x0144;
    private const int OldLicenseeCodeAddress = 0x014B;
    private const byte OldNintendoLicenseeCode = 0x01;
    private const byte NewLicenseeMarker = 0x33;
    private const byte NewNintendoLicenseeCode0 = 0x30;
    private const byte NewNintendoLicenseeCode1 = 0x31;
    private const byte Accumulator = 0x11;
    private const byte Flags = (byte)CpuFlag.Zero;
    private const ushort CgbRegisterBc = 0x0000;
    private const ushort CgbRegisterDe = 0xFF56;
    private const ushort CgbRegisterHl = 0x000D;
    private const ushort DmgCompatibilityRegisterDe = 0x0008;
    private const ushort DmgCompatibilityDefaultRegisterHl = 0x007C;
    private const ushort DmgCompatibilityLogoRegisterHl = 0x991A;
    private const ushort AudioRegistersStart = 0xFF10;

    private static readonly PostBootHardwareRegisterState[] _preAudioRegisters =
    [
        new(AddressMap.SerialTransferDataRegister, 0x00),
        new(AddressMap.SerialTransferControlRegister, 0x7F),
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
        new(AddressMap.VideoRamBankRegister, 0xFE),
        new(AddressMap.WorkRamBankRegister, 0xF8),
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
        PostBootState.SetCpuRegisters(
            cpu.Registers,
            CreateCpuRegisterState(operatingMode, cartridge)
        );
        PostBootState.SetHardwareRegisterStates(bus, _preAudioRegisters);
        ApplyAudioRegisters(bus);
        PostBootState.SetHardwareRegisterStates(bus, _commonPostAudioRegisters);

        if (operatingMode is not CgbOperatingMode.Cgb)
        {
            return;
        }

        PostBootState.SetHardwareRegisterStates(bus, _cgbModePostAudioRegisters);
        bus.Ppu.SetBackgroundColorPaletteRamToWhite();
    }

    private static PostBootCpuRegisterState CreateCpuRegisterState(
        CgbOperatingMode operatingMode,
        Cartridge cartridge
    ) =>
        operatingMode switch
        {
            CgbOperatingMode.Cgb => new PostBootCpuRegisterState(
                Accumulator,
                Flags,
                CgbRegisterBc,
                CgbRegisterDe,
                CgbRegisterHl,
                AddressMap.CartridgeEntryPointStart,
                AddressMap.HighRamEnd
            ),
            CgbOperatingMode.DmgCompatibility => CreateDmgCompatibilityCpuRegisterState(cartridge),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operatingMode),
                operatingMode,
                "Unsupported CGB operating mode."
            ),
        };

    private static PostBootCpuRegisterState CreateDmgCompatibilityCpuRegisterState(
        Cartridge cartridge
    )
    {
        byte registerB = CalculateDmgCompatibilityRegisterB(cartridge);
        ushort registerHl = registerB is 0x43 or 0x58
            ? DmgCompatibilityLogoRegisterHl
            : DmgCompatibilityDefaultRegisterHl;

        return new PostBootCpuRegisterState(
            Accumulator,
            Flags,
            (ushort)(registerB << 8),
            DmgCompatibilityRegisterDe,
            registerHl,
            AddressMap.CartridgeEntryPointStart,
            AddressMap.HighRamEnd
        );
    }

    private static byte CalculateDmgCompatibilityRegisterB(Cartridge cartridge)
    {
        if (!IsNintendoLicensee(cartridge))
        {
            return 0;
        }

        byte titleChecksum = 0;
        for (ushort address = TitleStartAddress; address <= TitleEndAddress; address++)
        {
            titleChecksum = unchecked((byte)(titleChecksum + cartridge.ReadRom(address)));
        }

        return titleChecksum;
    }

    private static bool IsNintendoLicensee(Cartridge cartridge)
    {
        byte oldLicenseeCode = cartridge.ReadRom(OldLicenseeCodeAddress);
        return oldLicenseeCode == OldNintendoLicenseeCode
            || (
                oldLicenseeCode == NewLicenseeMarker
                && cartridge.ReadRom(NewLicenseeCodeStartAddress) == NewNintendoLicenseeCode0
                && cartridge.ReadRom(NewLicenseeCodeStartAddress + 1) == NewNintendoLicenseeCode1
            );
    }

    private static void ApplyAudioRegisters(MemoryBus bus)
    {
        ReadOnlySpan<byte> values = AudioRegisterStates;
        for (int offset = 0; offset < values.Length; offset++)
        {
            bus.SetHardwareRegisterState((ushort)(AudioRegistersStart + offset), values[offset]);
        }
    }
}
