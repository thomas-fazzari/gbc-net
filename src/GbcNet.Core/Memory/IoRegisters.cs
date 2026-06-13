using GbcNet.Core.Apu;
using GbcNet.Core.Clock;
using GbcNet.Core.Dma;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;
using GbcNet.Core.Serial;
using GbcNet.Core.Timers;

namespace GbcNet.Core.Memory;

/// <summary>
/// Routes CPU-visible I/O registers at FF00-FF7F to their owning devices.
/// </summary>
internal sealed class IoRegisters(
    InterruptController interrupts,
    ClockController clock,
    JoypadController joypad,
    SerialController serial,
    ApuController apu,
    PpuController ppu,
    WorkRam workRam,
    CgbMiscRegisters cgbMiscRegisters,
    OamDmaController oamDma,
    CgbVramDmaController vramDma
)
{
    private readonly TimerController _timers = clock.Timers;

    /// <summary>
    /// Reads a CPU-visible I/O register.
    /// </summary>
    public byte Read(ushort address)
    {
        if (PpuController.ContainsRegister(address))
        {
            return ppu.ReadRegister(address);
        }

        if (ApuController.ContainsRegister(address))
        {
            return apu.ReadRegister(address);
        }

        if (CgbMiscRegisters.ContainsRegister(address))
        {
            return cgbMiscRegisters.ReadRegister(address);
        }

        return address switch
        {
            AddressMap.JoypadRegister => joypad.Read(),

            AddressMap.SerialTransferDataRegister => serial.TransferData,

            AddressMap.SerialTransferControlRegister => serial.ReadControl(),

            AddressMap.DividerRegister => clock.ReadDivider(),

            AddressMap.TimerCounterRegister => _timers.TimerCounter,

            AddressMap.TimerModuloRegister => _timers.TimerModulo,

            AddressMap.TimerControlRegister => _timers.ReadTimerControl(),

            AddressMap.InterruptFlagRegister => interrupts.ReadInterruptFlag(),

            AddressMap.Key1Register => clock.ReadKey1(),

            AddressMap.DmaRegister => oamDma.ReadRegister(),

            AddressMap.VideoRamDmaSourceHighRegister
            or AddressMap.VideoRamDmaSourceLowRegister
            or AddressMap.VideoRamDmaDestinationHighRegister
            or AddressMap.VideoRamDmaDestinationLowRegister
            or AddressMap.VideoRamDmaLengthModeStartRegister => vramDma.ReadRegister(address),

            AddressMap.VideoRamBankRegister
            or AddressMap.BackgroundPaletteIndexRegister
            or AddressMap.BackgroundPaletteDataRegister
            or AddressMap.ObjectPaletteIndexRegister
            or AddressMap.ObjectPaletteDataRegister
            or AddressMap.ObjectPriorityModeRegister => ppu.ReadRegister(address),

            AddressMap.WorkRamBankRegister => workRam.ReadBankRegister(),

            _ => 0xFF,
        };
    }

    private void Write(ushort address, byte value, IoRegisterWriteMode mode)
    {
        if (PpuController.ContainsRegister(address))
        {
            if (mode is IoRegisterWriteMode.CpuWrite)
            {
                ppu.WriteRegister(address, value);
            }
            else
            {
                ppu.SetRegisterState(address, value);
            }

            return;
        }

        if (ApuController.ContainsRegister(address))
        {
            if (mode is IoRegisterWriteMode.CpuWrite)
            {
                apu.WriteRegister(address, value);
            }
            else
            {
                apu.SetRegisterState(address, value);
            }

            return;
        }

        if (CgbMiscRegisters.ContainsRegister(address))
        {
            cgbMiscRegisters.WriteRegister(address, value);
            return;
        }

        switch (address)
        {
            case AddressMap.JoypadRegister:
                joypad.Write(
                    value,
                    requestInterruptOnTransition: mode is IoRegisterWriteMode.CpuWrite
                );
                return;

            case AddressMap.SerialTransferDataRegister:
                serial.TransferData = value;
                return;

            case AddressMap.SerialTransferControlRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    serial.WriteControl(value);
                }
                else
                {
                    serial.SetControlState(value);
                }
                return;

            case AddressMap.DividerRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    clock.ResetDivider();
                }
                else
                {
                    clock.SetDivider(value);
                }
                return;

            case AddressMap.Key1Register:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    clock.WriteKey1(value);
                }
                else
                {
                    clock.SetKey1State(value);
                }
                return;

            case AddressMap.TimerCounterRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    _timers.WriteTimerCounter(value);
                }
                else
                {
                    _timers.TimerCounter = value;
                }
                return;

            case AddressMap.TimerModuloRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    _timers.WriteTimerModulo(value);
                }
                else
                {
                    _timers.TimerModulo = value;
                }
                return;

            case AddressMap.TimerControlRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    _timers.WriteTimerControl(value);
                }
                else
                {
                    _timers.SetTimerControlState(value);
                }
                return;

            case AddressMap.InterruptFlagRegister:
                interrupts.SetInterruptFlag(value);
                return;

            case AddressMap.DmaRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    oamDma.StartOamTransfer(value);
                }
                else
                {
                    oamDma.SetRegisterState(value);
                }
                return;

            case AddressMap.VideoRamDmaSourceHighRegister:
            case AddressMap.VideoRamDmaSourceLowRegister:
            case AddressMap.VideoRamDmaDestinationHighRegister:
            case AddressMap.VideoRamDmaDestinationLowRegister:
            case AddressMap.VideoRamDmaLengthModeStartRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    vramDma.WriteRegister(address, value);
                }
                else
                {
                    vramDma.SetRegisterState(address, value);
                }
                return;

            case AddressMap.VideoRamBankRegister:
            case AddressMap.BackgroundPaletteIndexRegister:
            case AddressMap.BackgroundPaletteDataRegister:
            case AddressMap.ObjectPaletteIndexRegister:
            case AddressMap.ObjectPaletteDataRegister:
            case AddressMap.ObjectPriorityModeRegister:
                if (mode is IoRegisterWriteMode.CpuWrite)
                {
                    ppu.WriteRegister(address, value);
                }
                else
                {
                    ppu.SetRegisterState(address, value);
                }
                return;

            case AddressMap.WorkRamBankRegister:
                workRam.WriteBankRegister(value);
                return;

            default:
                return;
        }
    }

    /// <summary>
    /// Writes an I/O register as a CPU write, including side effects.
    /// </summary>
    public void WriteCpu(ushort address, byte value) =>
        Write(address, value, IoRegisterWriteMode.CpuWrite);

    /// <summary>
    /// Seeds an I/O register without CPU write side effects.
    /// </summary>
    public void SetState(ushort address, byte value) =>
        Write(address, value, IoRegisterWriteMode.SeedState);

    private enum IoRegisterWriteMode
    {
        CpuWrite = 0,
        SeedState = 1,
    }
}
