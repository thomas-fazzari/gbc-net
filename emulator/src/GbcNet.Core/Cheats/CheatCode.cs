// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Cheats;

/// <summary>
/// A parsed Game Boy cheat code.
/// </summary>
public readonly record struct CheatCode
{
    private readonly string? _canonicalCode;

    private CheatCode(
        CheatCodeType type,
        string canonicalCode,
        ushort address,
        byte value,
        byte? compareValue
    )
    {
        Type = type;
        _canonicalCode = canonicalCode;
        Address = address;
        Value = value;
        CompareValue = compareValue;
    }

    /// <summary>
    /// Cheat device family that defines this code's behavior.
    /// </summary>
    public CheatCodeType Type { get; }

    /// <summary>
    /// The canonical code text.
    /// </summary>
    public string CanonicalCode => _canonicalCode ?? string.Empty;

    /// <summary>
    /// CPU-visible address affected by the code.
    /// </summary>
    public ushort Address { get; }

    /// <summary>
    /// Replacement value returned by Game Genie or written by GameShark.
    /// </summary>
    public byte Value { get; }

    /// <summary>
    /// Optional original ROM value required by a Game Genie code.
    /// </summary>
    public byte? CompareValue { get; }

    internal bool IsValid => _canonicalCode is not null;

    /// <summary>
    /// Parses text as a code for the specified cheat device family.
    /// </summary>
    public static bool TryParse(CheatCodeType type, ReadOnlySpan<char> text, out CheatCode code)
    {
        switch (type)
        {
            case CheatCodeType.GameGenie:
                return TryParseGameGenie(text, out code);

            case CheatCodeType.GameShark:
                return TryParseGameShark(text, out code);

            default:
                code = default;
                return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => CanonicalCode;

    private static bool TryParseGameGenie(ReadOnlySpan<char> text, out CheatCode code)
    {
        text = text.Trim(' ');
        var hasCompareValue = text.Length is 9 or 11;
        var hasHyphens = text.Length is 7 or 11;

        if (
            text.Length is not (6 or 7 or 9 or 11)
            || (hasHyphens && (text[3] != '-' || (hasCompareValue && text[7] != '-')))
            || (!hasHyphens && text.IndexOf('-') >= 0)
        )
        {
            code = default;
            return false;
        }

        Span<char> canonical = stackalloc char[11];
        var digitCount = hasCompareValue ? 9 : 6;

        for (var digitIndex = 0; digitIndex < digitCount; digitIndex++)
        {
            var canonicalIndex = digitIndex + (digitIndex / 3);
            var digit = text[hasHyphens ? canonicalIndex : digitIndex];

            if (!char.IsAsciiHexDigit(digit))
            {
                code = default;
                return false;
            }

            canonical[canonicalIndex] = ToUpperAscii(digit);
        }

        canonical[3] = '-';
        if (hasCompareValue)
        {
            canonical[7] = '-';
        }

        var address = (ushort)(
            (
                (GetHexValue(canonical[6]) << 12)
                | (GetHexValue(canonical[2]) << 8)
                | (GetHexValue(canonical[4]) << 4)
                | GetHexValue(canonical[5])
            ) ^ 0xF000
        );

        if (address > AddressMap.RomEnd)
        {
            code = default;
            return false;
        }

        var replacementValue = (byte)((GetHexValue(canonical[0]) << 4) | GetHexValue(canonical[1]));
        byte? compareValue = null;

        if (hasCompareValue)
        {
            var encodedCompareValue = (byte)(
                (GetHexValue(canonical[8]) << 4) | GetHexValue(canonical[10])
            );
            compareValue = (byte)(((encodedCompareValue >> 2) | (encodedCompareValue << 6)) ^ 0xBA);
        }

        code = new CheatCode(
            CheatCodeType.GameGenie,
            new string(canonical[..(hasCompareValue ? 11 : 7)]),
            address,
            replacementValue,
            compareValue
        );
        return true;
    }

    private static bool TryParseGameShark(ReadOnlySpan<char> text, out CheatCode code)
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
            (GetHexValue(text[6]) << 12)
            | (GetHexValue(text[7]) << 8)
            | (GetHexValue(text[4]) << 4)
            | GetHexValue(text[5])
        );
        if (!IsSupportedGameSharkAddress(address))
        {
            code = default;
            return false;
        }

        Span<char> canonical = stackalloc char[8];
        for (var index = 0; index < canonical.Length; index++)
        {
            canonical[index] = ToUpperAscii(text[index]);
        }

        code = new CheatCode(
            CheatCodeType.GameShark,
            new string(canonical),
            address,
            (byte)((GetHexValue(text[2]) << 4) | GetHexValue(text[3])),
            compareValue: null
        );
        return true;
    }

    private static bool IsSupportedGameSharkAddress(ushort address) =>
        address
            is (>= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd)
                or (>= AddressMap.WorkRamStart and <= AddressMap.ObjectAttributeMemoryEnd)
                or >= AddressMap.IoRegistersStart;

    private static char ToUpperAscii(char value) =>
        value is >= 'a' and <= 'f' ? (char)(value - 32) : value;

    private static int GetHexValue(char value)
    {
        value = ToUpperAscii(value);
        return value <= '9' ? value - '0' : value - 'A' + 10;
    }
}
