using System.Globalization;
using FluentResults;
using GbcNet.Core.Cartridges.Memory;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Creates the cartridge memory controller matching the cartridge header type.
/// </summary>
internal static class CartridgeMemoryControllerFactory
{
    /// <summary>
    /// Creates the controller that handles ROM banking, external RAM, and MBC registers.
    /// </summary>
    public static Result<ICartridgeMemoryController> Create(byte[] rom, CartridgeHeader header) =>
        header.CartridgeType switch
        {
            CartridgeType.RomOnly => Result.Ok<ICartridgeMemoryController>(
                new RomOnlyMemoryController(rom)
            ),
            CartridgeType.Mbc1 or CartridgeType.Mbc1Ram or CartridgeType.Mbc1RamBattery =>
                Result.Ok<ICartridgeMemoryController>(
                    new Mbc1MemoryController(
                        rom,
                        header,
                        header.CartridgeType is CartridgeType.Mbc1RamBattery
                    )
                ),
            CartridgeType.Mbc5 or CartridgeType.Mbc5Ram or CartridgeType.Mbc5RamBattery =>
                Result.Ok<ICartridgeMemoryController>(
                    new Mbc5MemoryController(
                        rom,
                        header,
                        header.CartridgeType is CartridgeType.Mbc5RamBattery
                    )
                ),
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
