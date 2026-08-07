// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils.ResultObservers;

/// <summary>
/// Mooneye test-rom report sequences: a Fibonacci-like prefix signals pass,
/// a run of the failure byte signals fail.
/// </summary>
internal static class MooneyeReport
{
    public const byte FailureByte = 0x42;

    public static readonly byte[] PassReport = [0x03, 0x05, 0x08, 0x0D, 0x15, 0x22];

    public static readonly byte[] FailReport = [.. Enumerable.Repeat(FailureByte, 6)];
}
