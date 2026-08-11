// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Media;

namespace GbcNet.App.Shell.Chrome;

internal static class AppChrome
{
    public const string Bg = "SysCanvasBrush";
    public const string Text = "SysTextBrush";
    public const string Muted = "SysTextMutedBrush";
    public const string Status = "SysTextMutedBrush";
    public const string Error = "SysDangerBrush";

    public static IBrush Brush(string resourceKey) =>
        Application.Current?.Resources[resourceKey] as IBrush
        ?? throw new InvalidOperationException(
            $"Application brush resource '{resourceKey}' was not found."
        );
}
