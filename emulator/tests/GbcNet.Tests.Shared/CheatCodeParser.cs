// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;

namespace GbcNet.Tests.Shared;

/// <summary>
/// Parses a cheat-code string, asserting that parsing succeeds.
/// </summary>
internal static class CheatCodeParser
{
    public static CheatCode Parse(CheatCodeType type, string text)
    {
        CheatCode.TryParse(type, text, out var code).Should().BeTrue();
        return code;
    }
}
