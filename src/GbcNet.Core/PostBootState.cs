using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Applies the hardware state observed after skipping the boot ROM hand-off.
/// </summary>
internal static class PostBootState
{
    private const byte DmgDivider = 0xAB;
    private const byte DmgAccumulator = 0x01;
    private const byte DmgFlagsBase = (byte)CpuFlag.Zero;
    private const byte DmgFlagsChecksumNonZero = (byte)(CpuFlag.HalfCarry | CpuFlag.Carry);
    private const byte DmgRegisterB = 0x00;
    private const byte DmgRegisterC = 0x13;
    private const byte DmgRegisterD = 0x00;
    private const byte DmgRegisterE = 0xD8;
    private const byte DmgRegisterH = 0x01;
    private const byte DmgRegisterL = 0x4D;
    private const byte DmgJoypad = 0xCF;
    private const byte DmgSerialTransferData = 0x00;
    private const byte DmgSerialTransferControl = 0x7E;
    private const byte DmgInterruptFlag = 0x01;
    private const byte DmgLcdControl = 0x91;
    private const byte DmgLcdStatus = 0x85;
    private const byte DmgBackgroundPalette = 0xFC;
    private const byte DmgDma = 0xFF;

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
        registers.B = DmgRegisterB;
        registers.C = DmgRegisterC;
        registers.D = DmgRegisterD;
        registers.E = DmgRegisterE;
        registers.H = DmgRegisterH;
        registers.L = DmgRegisterL;
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
        bus.SetHardwareRegisterState(AddressMap.DividerRegister, DmgDivider);
        bus.SetHardwareRegisterState(AddressMap.TimerCounterRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.TimerModuloRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.TimerControlRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.InterruptFlagRegister, DmgInterruptFlag);
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
}
