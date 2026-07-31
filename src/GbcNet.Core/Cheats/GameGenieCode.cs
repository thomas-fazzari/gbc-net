// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cheats;

/// <summary>
/// A parsed Game Genie ROM read replacement code.
/// </summary>
public readonly record struct GameGenieCode
{
    private readonly string? _canonicalCode;

    private GameGenieCode(
        string canonicalCode,
        ushort address,
        byte replacementValue,
        byte? compareValue
    )
    {
        _canonicalCode = canonicalCode;
        Address = address;
        ReplacementValue = replacementValue;
        CompareValue = compareValue;
    }

    /// <summary>
    /// The uppercase, hyphenated code text.
    /// </summary>
    public string CanonicalCode => _canonicalCode ?? string.Empty;

    /// <summary>
    /// The CPU-visible ROM address to replace.
    /// </summary>
    public ushort Address { get; }

    /// <summary>
    /// The byte returned for a matching ROM read.
    /// </summary>
    public byte ReplacementValue { get; }

    /// <summary>
    /// The optional original byte required for a match.
    /// </summary>
    public byte? CompareValue { get; }

    internal bool IsValid => _canonicalCode is not null;

    /// <summary>
    /// Parses a compact or hyphenated six- or nine-digit Game Genie code.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out GameGenieCode code)
    {
        text = text.Trim(' ');
        var hasCompareValue = text.Length is 9 or 11;
        var hasHyphens = text.Length is 7 or 11;

        if (text.Length is not (6 or 7 or 9 or 11))
        {
            code = default;
            return false;
        }
        if (
            (hasHyphens && (text[3] != '-' || (hasCompareValue && text[7] != '-')))
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

        if (address > 0x7FFF)
        {
            code = default;
            return false;
        }

        var canonicalCode = new string(canonical[..(hasCompareValue ? 11 : 7)]);
        var replacementValue = (byte)((GetHexValue(canonical[0]) << 4) | GetHexValue(canonical[1]));
        byte? compareValue = null;

        if (hasCompareValue)
        {
            var encodedCompareValue = (byte)(
                (GetHexValue(canonical[8]) << 4) | GetHexValue(canonical[10])
            );
            compareValue = (byte)(((encodedCompareValue >> 2) | (encodedCompareValue << 6)) ^ 0xBA);
        }

        code = new GameGenieCode(canonicalCode, address, replacementValue, compareValue);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => CanonicalCode;

    private static char ToUpperAscii(char value) =>
        value is >= 'a' and <= 'f' ? (char)(value - 32) : value;

    private static int GetHexValue(char value) => value <= '9' ? value - '0' : value - 'A' + 10;
}
