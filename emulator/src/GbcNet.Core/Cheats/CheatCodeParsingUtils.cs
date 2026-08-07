// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cheats;

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
