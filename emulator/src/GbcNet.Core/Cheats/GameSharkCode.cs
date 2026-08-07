// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Cheats;

/// <summary>
/// A parsed GameShark VBlank memory write code.
/// </summary>
public readonly record struct GameSharkCode
{
    private readonly string? _canonicalCode;

    private GameSharkCode(string canonicalCode, ushort address, byte value)
    {
        _canonicalCode = canonicalCode;
        Address = address;
        Value = value;
    }

    /// <summary>
    /// The uppercase eight-digit code text.
    /// </summary>
    public string CanonicalCode => _canonicalCode ?? string.Empty;

    /// <summary>
    /// The CPU-visible address written at VBlank entry.
    /// </summary>
    public ushort Address { get; }

    /// <summary>
    /// The byte written at VBlank entry.
    /// </summary>
    public byte Value { get; }

    /// <summary>
    /// Parses an eight-digit 01VVLLHH GameShark memory write code.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out GameSharkCode code)
    {
        if (
            text.Length != 8
            || text[0] != '0'
            || text[1] != '1'
            || !char.IsAsciiHexDigit(text[2])
            || !char.IsAsciiHexDigit(text[3])
            || !char.IsAsciiHexDigit(text[4])
            || !char.IsAsciiHexDigit(text[5])
            || !char.IsAsciiHexDigit(text[6])
            || !char.IsAsciiHexDigit(text[7])
        )
        {
            code = default;
            return false;
        }

        var address = (ushort)(
            (CheatCodeParsingUtils.GetHexValue(text[6]) << 12)
            | (CheatCodeParsingUtils.GetHexValue(text[7]) << 8)
            | (CheatCodeParsingUtils.GetHexValue(text[4]) << 4)
            | CheatCodeParsingUtils.GetHexValue(text[5])
        );
        if (!IsSupportedWritableAddress(address))
        {
            code = default;
            return false;
        }

        Span<char> canonical = stackalloc char[8];
        for (var index = 0; index < canonical.Length; index++)
        {
            canonical[index] = CheatCodeParsingUtils.ToUpperAscii(text[index]);
        }

        code = new GameSharkCode(
            new string(canonical),
            address,
            (byte)(
                (CheatCodeParsingUtils.GetHexValue(text[2]) << 4)
                | CheatCodeParsingUtils.GetHexValue(text[3])
            )
        );
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => CanonicalCode;

    private static bool IsSupportedWritableAddress(ushort address) =>
        address
            is (>= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd)
                or (>= AddressMap.WorkRamStart and <= AddressMap.ObjectAttributeMemoryEnd)
                or >= AddressMap.IoRegistersStart;
}
