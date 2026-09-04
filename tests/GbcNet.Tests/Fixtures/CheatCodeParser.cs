// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;

namespace GbcNet.Tests.Fixtures;

/// <summary>
/// Parses a cheat-code string, asserting that parsing succeeds.
/// </summary>
internal static class CheatCodeParser
{
    /// <summary>
    /// Parses a cheat code and fails the current assertion if the text is invalid.
    /// </summary>
    /// <param name="type">The cheat-code format to parse.</param>
    /// <param name="text">The encoded cheat code.</param>
    /// <returns>The parsed cheat code.</returns>
    public static CheatCode Parse(CheatCodeType type, string text)
    {
        CheatCode.TryParse(type, text, out var code).Should().BeTrue();
        return code;
    }
}
