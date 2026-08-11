// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace GbcNet.App.Shell.Chrome;

internal static partial class WindowsTitleBar
{
    private const int CaptionButtonBoundsAttribute = 5;
    private const double DefaultCaptionButtonsWidth = 146;

    public static double GetCaptionButtonsWidth(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var windowHandle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (
            windowHandle == 0
            || DwmGetWindowAttribute(
                windowHandle,
                CaptionButtonBoundsAttribute,
                out var bounds,
                Marshal.SizeOf<NativeRect>()
            ) != 0
        )
        {
            return DefaultCaptionButtonsWidth;
        }

        var width = bounds.Right - bounds.Left;
        return width > 0 ? width / window.RenderScaling : DefaultCaptionButtonsWidth;
    }

    [LibraryImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int DwmGetWindowAttribute(
        nint windowHandle,
        int attribute,
        out NativeRect attributeValue,
        int attributeValueSize
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }
}
