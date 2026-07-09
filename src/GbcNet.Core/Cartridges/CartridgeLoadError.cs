// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;

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
    private CartridgeLoadResult(Cartridge? cartridge, CartridgeLoadError? error)
    {
        Cartridge = cartridge;
        Error = error;
    }

    /// <summary>
    /// Indicates that cartridge loading succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Cartridge))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Cartridge is not null;

    /// <summary>
    /// Loaded cartridge when <see cref="IsSuccess" /> is true.
    /// </summary>
    public Cartridge? Cartridge { get; }

    /// <summary>
    /// Typed load error when <see cref="IsSuccess" /> is false.
    /// </summary>
    public CartridgeLoadError? Error { get; }

    public static CartridgeLoadResult Success(Cartridge cartridge) => new(cartridge, null);

    public static CartridgeLoadResult Failure(CartridgeLoadError error) => new(null, error);
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
