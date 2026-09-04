// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;

namespace GbcNet.Tests.Unit.RomTesting.Utils;

/// <summary>
/// Computes stable hashes for ROM and golden-frame fixtures.
/// </summary>
internal static class RomTestHashing
{
    /// <summary>
    /// Computes a SHA-256 hash as uppercase hexadecimal text.
    /// </summary>
    public static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
