using System.Globalization;
using FluentResults;
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

    private Cartridge(
        byte[] rom,
        CartridgeHeader header,
        ICartridgeMemoryController memoryController
    )
    {
        Header = header;
        _memoryController = memoryController;
        RomLength = rom.Length;
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
        Result<ICartridgeMemoryController> memoryControllerResult = CreateMemoryController(
            romBytes,
            header
        );

        return memoryControllerResult.IsFailed
            ? Result.Fail<Cartridge>(memoryControllerResult.Errors)
            : Result.Ok(new Cartridge(romBytes, header, memoryControllerResult.Value));
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

    private static ushort GetExternalRamOffset(ushort address) =>
        (ushort)(address - AddressMap.ExternalRamStart);

    private static Result<ICartridgeMemoryController> CreateMemoryController(
        byte[] rom,
        CartridgeHeader header
    ) =>
        header.CartridgeType switch
        {
            CartridgeType.RomOnly => Result.Ok<ICartridgeMemoryController>(
                new RomOnlyMemoryController(rom)
            ),
            CartridgeType.Mbc1 or CartridgeType.Mbc1Ram or CartridgeType.Mbc1RamBattery =>
                Result.Ok<ICartridgeMemoryController>(new Mbc1MemoryController(rom, header)),
            CartridgeType.Mbc5 or CartridgeType.Mbc5Ram or CartridgeType.Mbc5RamBattery =>
                Result.Ok<ICartridgeMemoryController>(new Mbc5MemoryController(rom, header)),
            _ => Result.Fail<ICartridgeMemoryController>(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedCartridgeType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cartridge type 0x{0:X2} is not supported yet.",
                        (int)header.CartridgeType
                    )
                )
            ),
        };
}
