namespace GbcNet.Core.Memory;

/// <summary>
/// Defines the 16-bit Game Boy memory map ranges from Pan Docs.
/// </summary>
internal static class AddressMap
{
    /// <summary>
    /// First address of the cartridge ROM window.
    /// </summary>
    public const ushort RomStart = 0x0000;

    /// <summary>
    /// Last address of the cartridge ROM window.
    /// </summary>
    public const ushort RomEnd = 0x7FFF;

    /// <summary>
    /// First address of video RAM.
    /// </summary>
    public const ushort VideoRamStart = 0x8000;

    /// <summary>
    /// Last address of video RAM.
    /// </summary>
    public const ushort VideoRamEnd = 0x9FFF;

    /// <summary>
    /// First address of cartridge external RAM.
    /// </summary>
    public const ushort ExternalRamStart = 0xA000;

    /// <summary>
    /// Last address of cartridge external RAM.
    /// </summary>
    public const ushort ExternalRamEnd = 0xBFFF;

    /// <summary>
    /// First address of work RAM.
    /// </summary>
    public const ushort WorkRamStart = 0xC000;

    /// <summary>
    /// Last address of directly addressable work RAM.
    /// </summary>
    public const ushort WorkRamEnd = 0xDFFF;

    /// <summary>
    /// First address of echo RAM, mirroring work RAM at C000-DDFF.
    /// </summary>
    public const ushort EchoRamStart = 0xE000;

    /// <summary>
    /// Last address of echo RAM.
    /// </summary>
    public const ushort EchoRamEnd = 0xFDFF;

    /// <summary>
    /// First address of object attribute memory.
    /// </summary>
    public const ushort ObjectAttributeMemoryStart = 0xFE00;

    /// <summary>
    /// Last address of object attribute memory.
    /// </summary>
    public const ushort ObjectAttributeMemoryEnd = 0xFE9F;

    /// <summary>
    /// First address of the prohibited not-usable range.
    /// </summary>
    public const ushort NotUsableStart = 0xFEA0;

    /// <summary>
    /// Last address of the prohibited not-usable range.
    /// </summary>
    public const ushort NotUsableEnd = 0xFEFF;

    /// <summary>
    /// First address of I/O registers.
    /// </summary>
    public const ushort IoRegistersStart = 0xFF00;

    /// <summary>
    /// Last address of I/O registers.
    /// </summary>
    public const ushort IoRegistersEnd = 0xFF7F;

    /// <summary>
    /// First address of high RAM.
    /// </summary>
    public const ushort HighRamStart = 0xFF80;

    /// <summary>
    /// Last address of high RAM.
    /// </summary>
    public const ushort HighRamEnd = 0xFFFE;

    /// <summary>
    /// Address of the interrupt enable register.
    /// </summary>
    public const ushort InterruptEnableRegister = 0xFFFF;
}
