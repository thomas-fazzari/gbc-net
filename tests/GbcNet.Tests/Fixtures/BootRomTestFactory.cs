// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;

namespace GbcNet.Tests.Fixtures;

/// <summary>
/// Creates zero-filled DMG, packed CGB, and SGB boot ROM byte arrays for tests.
/// </summary>
internal static class BootRomTestFactory
{
    /// <summary>
    /// Creates a zero-filled DMG boot ROM and optionally edits its bytes.
    /// </summary>
    /// <param name="configure">An optional callback invoked with the new ROM.</param>
    /// <returns>A boot ROM with the exact DMG size.</returns>
    public static byte[] CreateDmg(Action<byte[]>? configure = null) =>
        Create(BootRomOptions.DmgBootRomSize, configure);

    /// <summary>
    /// Creates a DMG boot ROM with <paramref name="marker"/> at address zero.
    /// </summary>
    public static byte[] CreateDmg(byte marker) => CreateDmg(bytes => bytes[0] = marker);

    /// <summary>
    /// Creates a zero-filled CGB boot ROM and optionally edits its bytes.
    /// </summary>
    /// <param name="configure">An optional callback invoked with the new ROM.</param>
    /// <returns>A packed 2,048-byte CGB boot ROM with its unused gap omitted.</returns>
    public static byte[] CreateCgb(Action<byte[]>? configure = null) =>
        Create(BootRomOptions.CgbBootRomSize, configure);

    /// <summary>
    /// Creates a CGB boot ROM with <paramref name="marker"/> at address zero.
    /// </summary>
    public static byte[] CreateCgb(byte marker) => CreateCgb(bytes => bytes[0] = marker);

    /// <summary>
    /// Creates a zero-filled SGB boot ROM and optionally edits its bytes.
    /// </summary>
    /// <param name="configure">An optional callback invoked with the new ROM.</param>
    /// <returns>A boot ROM with the exact SGB size.</returns>
    public static byte[] CreateSgb(Action<byte[]>? configure = null) =>
        Create(BootRomOptions.SgbBootRomSize, configure);

    /// <summary>
    /// Creates an SGB boot ROM with <paramref name="marker"/> at address zero.
    /// </summary>
    public static byte[] CreateSgb(byte marker) => CreateSgb(bytes => bytes[0] = marker);

    private static byte[] Create(int length, Action<byte[]>? configure)
    {
        var bytes = new byte[length];
        configure?.Invoke(bytes);
        return bytes;
    }
}
