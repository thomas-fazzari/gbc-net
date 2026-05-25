using FluentResults;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Error returned when cartridge bytes cannot be loaded as a supported ROM.
/// </summary>
public sealed class CartridgeLoadError(CartridgeLoadErrorCode code, string message) : Error(message)
{
    public CartridgeLoadErrorCode Code { get; } = code;
}

public enum CartridgeLoadErrorCode
{
    /// <summary>
    /// ROM does not contain the full cartridge header.
    /// </summary>
    RomTooSmall = 0,

    /// <summary>
    /// Header checksum byte does not match bytes 0134-014C.
    /// </summary>
    InvalidHeaderChecksum = 1,

    /// <summary>
    /// Cartridge mapper or hardware type is not implemented yet.
    /// </summary>
    UnsupportedCartridgeType = 2,

    /// <summary>
    /// ROM size code is not known or supported.
    /// </summary>
    UnsupportedRomSize = 3,

    /// <summary>
    /// RAM size code is not known or supported.
    /// </summary>
    UnsupportedRamSize = 4,

    /// <summary>
    /// Actual ROM length differs from the size declared in the header.
    /// </summary>
    RomLengthMismatch = 5,
}
