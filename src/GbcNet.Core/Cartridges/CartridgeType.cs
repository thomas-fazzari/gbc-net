// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Cartridge hardware type declared by header byte 0147.
/// </summary>
public enum CartridgeType
{
    /// <summary>
    /// Plain 32 KiB ROM with no memory bank controller.
    /// </summary>
    RomOnly = 0x00,

    /// <summary>
    /// MBC1 memory bank controller without external RAM.
    /// </summary>
    Mbc1 = 0x01,

    /// <summary>
    /// MBC1 memory bank controller with external RAM.
    /// </summary>
    Mbc1Ram = 0x02,

    /// <summary>
    /// MBC1 memory bank controller with battery-backed external RAM.
    /// </summary>
    Mbc1RamBattery = 0x03,

    /// <summary>
    /// MBC2 memory bank controller with built-in 512 x 4-bit RAM.
    /// </summary>
    Mbc2 = 0x05,

    /// <summary>
    /// MBC2 memory bank controller with battery-backed built-in RAM.
    /// </summary>
    Mbc2Battery = 0x06,

    /// <summary>
    /// ROM with external RAM and no standard MBC.
    /// </summary>
    RomRam = 0x08,

    /// <summary>
    /// ROM with battery-backed external RAM and no standard MBC.
    /// </summary>
    RomRamBattery = 0x09,

    /// <summary>
    /// MBC3 memory bank controller with RTC and battery, without external RAM.
    /// </summary>
    Mbc3TimerBattery = 0x0F,

    /// <summary>
    /// MBC3 memory bank controller with RTC, external RAM, and battery.
    /// </summary>
    Mbc3TimerRamBattery = 0x10,

    /// <summary>
    /// MBC3 memory bank controller without external RAM.
    /// </summary>
    Mbc3 = 0x11,

    /// <summary>
    /// MBC3 memory bank controller with external RAM.
    /// </summary>
    Mbc3Ram = 0x12,

    /// <summary>
    /// MBC3 memory bank controller with battery-backed external RAM.
    /// </summary>
    Mbc3RamBattery = 0x13,

    /// <summary>
    /// MBC5 memory bank controller without external RAM.
    /// </summary>
    Mbc5 = 0x19,

    /// <summary>
    /// MBC5 memory bank controller with external RAM.
    /// </summary>
    Mbc5Ram = 0x1A,

    /// <summary>
    /// MBC5 memory bank controller with battery-backed external RAM.
    /// </summary>
    Mbc5RamBattery = 0x1B,
}

internal static class CartridgeTypeExtensions
{
    extension(CartridgeType cartridgeType)
    {
        public bool IsNoMbc() =>
            cartridgeType
                is CartridgeType.RomOnly
                    or CartridgeType.RomRam
                    or CartridgeType.RomRamBattery;

        public bool IsMbc1() =>
            cartridgeType
                is CartridgeType.Mbc1
                    or CartridgeType.Mbc1Ram
                    or CartridgeType.Mbc1RamBattery;

        public bool IsMbc2() => cartridgeType is CartridgeType.Mbc2 or CartridgeType.Mbc2Battery;

        public bool IsMbc3() =>
            cartridgeType
                is CartridgeType.Mbc3TimerBattery
                    or CartridgeType.Mbc3TimerRamBattery
                    or CartridgeType.Mbc3
                    or CartridgeType.Mbc3Ram
                    or CartridgeType.Mbc3RamBattery;

        public bool IsMbc5() =>
            cartridgeType
                is CartridgeType.Mbc5
                    or CartridgeType.Mbc5Ram
                    or CartridgeType.Mbc5RamBattery;

        public bool HasRtc() =>
            cartridgeType is CartridgeType.Mbc3TimerBattery or CartridgeType.Mbc3TimerRamBattery;

        public bool HasBatteryBackedExternalRam() =>
            cartridgeType
                is CartridgeType.RomRamBattery
                    or CartridgeType.Mbc1RamBattery
                    or CartridgeType.Mbc3TimerRamBattery
                    or CartridgeType.Mbc3RamBattery
                    or CartridgeType.Mbc5RamBattery;
    }
}
