// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Error returned when cartridge bytes cannot be loaded as a supported ROM.
/// </summary>
public sealed class CartridgeLoadError(CartridgeLoadErrorCode code, string message)
{
    /// <summary>
    /// Machine-readable category for a cartridge load failure.
    /// </summary>
    public CartridgeLoadErrorCode Code { get; } = code;

    /// <summary>
    /// User-facing failure message.
    /// </summary>
    public string Message { get; } = message;
}

/// <summary>
/// Result returned when loading cartridge bytes.
/// </summary>
public sealed class CartridgeLoadResult
{
    private readonly Cartridge? _cartridge;
    private readonly CartridgeLoadError? _error;

    private CartridgeLoadResult(Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);

        _cartridge = cartridge;
        IsSuccess = true;
    }

    private CartridgeLoadResult(CartridgeLoadError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        _error = error;
    }

    /// <summary>
    /// Indicates that cartridge loading succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates that cartridge loading failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Loaded cartridge. Throws when <see cref="IsSuccess" /> is false.
    /// </summary>
    public Cartridge Cartridge =>
        _cartridge ?? throw new InvalidOperationException("Cartridge load did not succeed.");

    /// <summary>
    /// Typed load error. Throws when <see cref="IsFailure" /> is false.
    /// </summary>
    public CartridgeLoadError Error =>
        _error ?? throw new InvalidOperationException("Cartridge load did not fail.");

    public static CartridgeLoadResult Success(Cartridge cartridge) => new(cartridge);

    public static CartridgeLoadResult Failure(CartridgeLoadError error) => new(error);
}

/// <summary>
/// Typed cartridge load failure reasons.
/// </summary>
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
    /// Cartridge memory controller or hardware type is not implemented yet.
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
