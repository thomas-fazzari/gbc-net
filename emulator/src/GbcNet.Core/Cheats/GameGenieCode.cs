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

            canonical[canonicalIndex] = CheatCodeParsingUtils.ToUpperAscii(digit);
        }

        canonical[3] = '-';
        if (hasCompareValue)
        {
            canonical[7] = '-';
        }

        var address = (ushort)(
            (
                (CheatCodeParsingUtils.GetHexValue(canonical[6]) << 12)
                | (CheatCodeParsingUtils.GetHexValue(canonical[2]) << 8)
                | (CheatCodeParsingUtils.GetHexValue(canonical[4]) << 4)
                | CheatCodeParsingUtils.GetHexValue(canonical[5])
            ) ^ 0xF000
        );

        if (address > 0x7FFF)
        {
            code = default;
            return false;
        }

        var canonicalCode = new string(canonical[..(hasCompareValue ? 11 : 7)]);
        var replacementValue = (byte)(
            (CheatCodeParsingUtils.GetHexValue(canonical[0]) << 4)
            | CheatCodeParsingUtils.GetHexValue(canonical[1])
        );
        byte? compareValue = null;

        if (hasCompareValue)
        {
            var encodedCompareValue = (byte)(
                (CheatCodeParsingUtils.GetHexValue(canonical[8]) << 4)
                | CheatCodeParsingUtils.GetHexValue(canonical[10])
            );
            compareValue = (byte)(((encodedCompareValue >> 2) | (encodedCompareValue << 6)) ^ 0xBA);
        }

        code = new GameGenieCode(canonicalCode, address, replacementValue, compareValue);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => CanonicalCode;
}
