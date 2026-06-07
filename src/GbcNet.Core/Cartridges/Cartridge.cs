using System.Globalization;
using FluentResults;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Immutable loaded Game Boy cartridge ROM.
/// </summary>
public sealed class Cartridge
{
    public const int FixedRomBankSize = 16 * 1024;
    public const int AddressableRomSize = 2 * FixedRomBankSize;

    private readonly ICartridgeMemoryController _memoryController;

    private Cartridge(CartridgeHeader header, ICartridgeMemoryController memoryController)
    {
        Header = header;
        _memoryController = memoryController;
        RomLength = header.RomSizeBytes;
    }

    /// <summary>
    /// Parsed cartridge header metadata.
    /// </summary>
    public CartridgeHeader Header { get; }

    /// <summary>
    /// Full ROM payload length, in bytes.
    /// </summary>
    public int RomLength { get; }

    /// <summary>
    /// Indicates whether the cartridge has battery-backed external RAM.
    /// </summary>
    public bool HasBatteryBackedRam => _memoryController.ExternalRam.HasBatteryBackedRam;

    /// <summary>
    /// Battery-backed external RAM size, in bytes.
    /// </summary>
    public int BatteryRamSize => _memoryController.ExternalRam.BatteryRamSize;

    /// <summary>
    /// Indicates whether battery-backed RAM changed since load or the last clear.
    /// </summary>
    public bool IsBatteryRamDirty => _memoryController.ExternalRam.IsBatteryRamDirty;

    /// <summary>
    /// Parses and loads a cartridge image.
    /// </summary>
    /// <returns>
    /// A loaded cartridge, or a typed cartridge load error.
    /// </returns>
    public static Result<Cartridge> Load(ReadOnlySpan<byte> rom)
    {
        Result<CartridgeHeader> headerResult = CartridgeHeader.Parse(rom);
        if (headerResult.IsFailed)
        {
            return Result.Fail<Cartridge>(headerResult.Errors);
        }

        CartridgeHeader header = headerResult.Value;
        if (rom.Length != header.RomSizeBytes)
        {
            return Result.Fail<Cartridge>(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.RomLengthMismatch,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ROM length is {0} bytes, but header declares {1} bytes.",
                        rom.Length,
                        header.RomSizeBytes
                    )
                )
            );
        }

        byte[] romBytes = rom.ToArray();
        Result<ICartridgeMemoryController> memoryControllerResult =
            CartridgeMemoryControllerFactory.Create(romBytes, header);

        return memoryControllerResult.IsFailed
            ? Result.Fail<Cartridge>(memoryControllerResult.Errors)
            : Result.Ok(new Cartridge(header, memoryControllerResult.Value));
    }

    /// <summary>
    /// Reads from the cartridge ROM address window at 0000-7FFF.
    /// </summary>
    /// <param name="address">
    /// CPU-visible cartridge ROM address.
    /// </param>
    /// <returns>
    /// The ROM byte at the requested address.
    /// </returns>
    public byte ReadRom(ushort address)
    {
        if (address >= AddressableRomSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge ROM address must be in the 0000-7FFF range."
            );
        }

        return _memoryController.ReadRom(address);
    }

    /// <summary>
    /// Writes to the cartridge ROM window, where MBC registers are mapped.
    /// </summary>
    public void WriteRom(ushort address, byte value)
    {
        if (address >= AddressableRomSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge ROM address must be in the 0000-7FFF range."
            );
        }

        _memoryController.WriteRom(address, value);
    }

    /// <summary>
    /// Reads from cartridge external RAM at A000-BFFF.
    /// </summary>
    public byte ReadRam(ushort address)
    {
        if (address is < AddressMap.ExternalRamStart or > AddressMap.ExternalRamEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge RAM address must be in the A000-BFFF range."
            );
        }

        return ReadRamOffset(GetExternalRamOffset(address));
    }

    /// <summary>
    /// Writes to cartridge external RAM at A000-BFFF.
    /// </summary>
    public void WriteRam(ushort address, byte value)
    {
        if (address is < AddressMap.ExternalRamStart or > AddressMap.ExternalRamEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge RAM address must be in the A000-BFFF range."
            );
        }

        _memoryController.WriteRamOffset(GetExternalRamOffset(address), value);
    }

    /// <summary>
    /// Reads from cartridge external RAM using a cartridge-local 8 KiB bank offset.
    /// </summary>
    internal byte ReadRamOffset(ushort offset)
    {
        if (offset >= AddressMap.ExternalRamWindowSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "Cartridge RAM offset must be in the 0000-1FFF range."
            );
        }

        return _memoryController.ReadRamOffset(offset);
    }

    /// <summary>
    /// Copies battery-backed external RAM for persistence.
    /// </summary>
    public byte[] ExportBatteryRam() => _memoryController.ExternalRam.ExportBatteryRam();

    /// <summary>
    /// Loads persisted battery-backed external RAM into the cartridge.
    /// </summary>
    public Result ImportBatteryRam(ReadOnlySpan<byte> data) =>
        _memoryController.ExternalRam.ImportBatteryRam(data);

    /// <summary>
    /// Marks battery-backed external RAM as persisted.
    /// </summary>
    public void ClearBatteryRamDirty()
    {
        _memoryController.ExternalRam.ClearBatteryRamDirty();
    }

    private static ushort GetExternalRamOffset(ushort address) =>
        (ushort)(address - AddressMap.ExternalRamStart);
}
