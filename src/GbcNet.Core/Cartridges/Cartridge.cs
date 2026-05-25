using System.Globalization;
using FluentResults;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Immutable loaded Game Boy cartridge ROM.
/// </summary>
public sealed class Cartridge
{
    public const int FixedRomBankSize = 16 * 1024;
    public const int AddressableRomSize = 2 * FixedRomBankSize;

    private readonly byte[] _rom;

    private Cartridge(byte[] rom, CartridgeHeader header)
    {
        _rom = rom;
        Header = header;
    }

    /// <summary>
    /// Parsed cartridge header metadata.
    /// </summary>
    public CartridgeHeader Header { get; }

    /// <summary>
    /// Full ROM payload length, in bytes.
    /// </summary>
    public int RomLength => _rom.Length;

    /// <summary>
    /// Parses and loads a ROM-only cartridge image.
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
        if (header.CartridgeType != CartridgeType.RomOnly)
        {
            return Result.Fail<Cartridge>(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedCartridgeType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cartridge type 0x{0:X2} is not supported yet.",
                        (int)header.CartridgeType
                    )
                )
            );
        }

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

        return Result.Ok(new Cartridge(rom.ToArray(), header));
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

        return _rom[address];
    }
}
