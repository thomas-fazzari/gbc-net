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
    public static Result<ICartridgeMemoryController> Create(byte[] rom, CartridgeHeader header)
    {
        CartridgeType cartridgeType = header.CartridgeType;

        return cartridgeType switch
        {
            _ when cartridgeType.IsNoMbc() => Result.Ok<ICartridgeMemoryController>(
                new NoMbcMemoryController(rom, header, cartridgeType.HasBatteryBackedRam())
            ),
            _ when cartridgeType.IsMbc1() => Result.Ok<ICartridgeMemoryController>(
                new Mbc1MemoryController(rom, header, cartridgeType.HasBatteryBackedRam())
            ),
            _ when cartridgeType.IsMbc2() => Result.Ok<ICartridgeMemoryController>(
                new Mbc2MemoryController(rom, header, cartridgeType.HasBatteryBackedRam())
            ),
            _ when cartridgeType.IsMbc3() => Result.Ok<ICartridgeMemoryController>(
                new Mbc3MemoryController(rom, header, cartridgeType.HasBatteryBackedRam())
            ),
            _ when cartridgeType.IsMbc5() => Result.Ok<ICartridgeMemoryController>(
                new Mbc5MemoryController(rom, header, cartridgeType.HasBatteryBackedRam())
            ),
            _ => Result.Fail<ICartridgeMemoryController>(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedCartridgeType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cartridge type 0x{0:X2} is not supported yet.",
                        (int)cartridgeType
                    )
                )
            ),
        };
    }
}
