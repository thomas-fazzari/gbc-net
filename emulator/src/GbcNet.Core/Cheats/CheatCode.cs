// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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
            case CheatCodeType.GameGenie when GameGenieCode.TryParse(text, out var gameGenieCode):
                code = new CheatCode(
                    type,
                    gameGenieCode.CanonicalCode,
                    gameGenieCode.Address,
                    gameGenieCode.ReplacementValue,
                    gameGenieCode.CompareValue
                );
                return true;

            case CheatCodeType.GameShark when GameSharkCode.TryParse(text, out var gameSharkCode):
                code = new CheatCode(
                    type,
                    gameSharkCode.CanonicalCode,
                    gameSharkCode.Address,
                    gameSharkCode.Value,
                    compareValue: null
                );
                return true;

            default:
                code = default;
                return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => CanonicalCode;
}

/// <summary>
/// Shared parsing helpers for Game Genie and GameShark code text.
/// </summary>
internal static class CheatCodeParsingUtils
{
    /// <summary>
    /// Converts lowercase hex digits a-f to uppercase; leaves all other characters unchanged.
    /// </summary>
    public static char ToUpperAscii(char value) =>
        value is >= 'a' and <= 'f' ? (char)(value - 32) : value;

    /// <summary>
    /// Returns the numeric value of a hex digit (0-9, A-F), uppercasing first if needed.
    /// </summary>
    public static int GetHexValue(char value)
    {
        value = ToUpperAscii(value);
        return value <= '9' ? value - '0' : value - 'A' + 10;
    }
}
