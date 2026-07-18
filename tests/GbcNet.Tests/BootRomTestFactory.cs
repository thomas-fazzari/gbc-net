// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;

namespace GbcNet.Tests;

internal static class BootRomTestFactory
{
    public static byte[] CreateDmg(Action<byte[]>? configure = null) =>
        Create(BootRomOptions.DmgBootRomSize, configure);

    public static byte[] CreateDmg(byte marker) => CreateDmg(bytes => bytes[0] = marker);

    public static byte[] CreateCgb(Action<byte[]>? configure = null) =>
        Create(BootRomOptions.CgbBootRomSize, configure);

    public static byte[] CreateCgb(byte marker) => CreateCgb(bytes => bytes[0] = marker);

    public static byte[] CreateSgb(Action<byte[]>? configure = null) =>
        Create(BootRomOptions.SgbBootRomSize, configure);

    public static byte[] CreateSgb(byte marker) => CreateSgb(bytes => bytes[0] = marker);

    private static byte[] Create(int length, Action<byte[]>? configure)
    {
        var bytes = new byte[length];
        configure?.Invoke(bytes);
        return bytes;
    }
}
