using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Applies the hardware state observed after skipping the boot ROM hand-off.
/// </summary>
internal static class PostBootState
{
    private const ushort DmgDividerCounter = 0xABCC;
    private const byte DmgAccumulator = 0x01;
    private const byte DmgFlagsBase = (byte)CpuFlag.Zero;
    private const byte DmgFlagsChecksumNonZero = (byte)(CpuFlag.HalfCarry | CpuFlag.Carry);
    private const ushort DmgRegisterBc = 0x0013;
    private const ushort DmgRegisterDe = 0x00D8;
    private const ushort DmgRegisterHl = 0x014D;
    private const byte DmgJoypad = 0xCF;
    private const byte DmgSerialTransferData = 0x00;
    private const byte DmgSerialTransferControl = 0x7E;
    private const byte DmgInterruptFlag = 0x01;
    private const byte DmgLcdControl = 0x91;
    private const byte DmgLcdStatus = 0x85;
    private const byte DmgBackgroundPalette = 0xFC;
    private const byte DmgDma = 0xFF;
    private const ushort AudioRegistersStart = 0xFF10;

    /// <summary>
    /// DMG post-boot APU register values indexed from FF10 through FF26.
    /// </summary>
    private static ReadOnlySpan<byte> DmgAudioRegisterStates =>
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
        HardwareModel hardwareModel,
        Cartridge cartridge,
        Cpu cpu,
        MemoryBus bus
    )
    {
        switch (hardwareModel)
        {
            case HardwareModel.Dmg:
                ApplyDmgCpuRegisters(cartridge, cpu.Registers);
                ApplyDmgIoRegisters(bus);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(hardwareModel),
                    hardwareModel,
                    message: null
                );
        }
    }

    private static void ApplyDmgCpuRegisters(Cartridge cartridge, Registers registers)
    {
        registers.A = DmgAccumulator;
        registers.F = cartridge.Header.HeaderChecksum is 0x00
            ? DmgFlagsBase
            : (byte)(DmgFlagsBase | DmgFlagsChecksumNonZero);
        registers.BC = DmgRegisterBc;
        registers.DE = DmgRegisterDe;
        registers.HL = DmgRegisterHl;
        registers.PC = AddressMap.CartridgeEntryPointStart;
        registers.SP = AddressMap.HighRamEnd;
    }

    private static void ApplyDmgIoRegisters(MemoryBus bus)
    {
        bus.SetHardwareRegisterState(AddressMap.JoypadRegister, DmgJoypad);
        bus.SetHardwareRegisterState(AddressMap.SerialTransferDataRegister, DmgSerialTransferData);
        bus.SetHardwareRegisterState(
            AddressMap.SerialTransferControlRegister,
            DmgSerialTransferControl
        );
        bus.Timers.SetDividerCounter(DmgDividerCounter);
        bus.SetHardwareRegisterState(AddressMap.TimerCounterRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.TimerModuloRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.TimerControlRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.InterruptFlagRegister, DmgInterruptFlag);
        ApplyDmgAudioRegisters(bus);
        bus.SetHardwareRegisterState(AddressMap.LcdControlRegister, DmgLcdControl);
        bus.SetHardwareRegisterState(AddressMap.LcdStatusRegister, DmgLcdStatus);
        bus.SetHardwareRegisterState(AddressMap.ScrollYRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.ScrollXRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.LcdYCoordinateRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.LcdYCompareRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.DmaRegister, DmgDma);
        bus.SetHardwareRegisterState(AddressMap.BackgroundPaletteRegister, DmgBackgroundPalette);
        bus.SetHardwareRegisterState(AddressMap.WindowYRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.WindowXRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.InterruptEnableRegister, 0x00);
    }

    private static void ApplyDmgAudioRegisters(MemoryBus bus)
    {
        ReadOnlySpan<byte> values = DmgAudioRegisterStates;
        for (int offset = 0; offset < values.Length; offset++)
        {
            bus.SetHardwareRegisterState((ushort)(AudioRegistersStart + offset), values[offset]);
        }
    }
}
