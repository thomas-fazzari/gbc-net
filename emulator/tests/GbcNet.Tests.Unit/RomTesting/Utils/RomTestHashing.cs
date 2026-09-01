// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;

namespace GbcNet.Tests.Unit.RomTesting.Utils;

internal static class RomTestHashing
{
    public static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
