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
