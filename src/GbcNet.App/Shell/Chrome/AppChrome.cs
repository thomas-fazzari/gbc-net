// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GbcNet.App.Shell.Chrome;

internal static class AppChrome
{
    public const string Bg = "ChromeBackgroundBrush";
    public const string Text = "ChromeTextBrush";
    public const string Muted = "ChromeMutedBrush";
    public const string Status = "ChromeStatusBrush";
    public const string Error = "ChromeErrorBrush";

    public static IBrush Brush(string resourceKey) =>
        Application.Current?.Resources[resourceKey] as IBrush
        ?? throw new InvalidOperationException(
            $"Application brush resource '{resourceKey}' was not found."
        );

    public static Button Button(string text, bool accent = false)
    {
        var button = new Button { Content = text };
        button.Classes.Add("chrome-button");
        if (accent)
        {
            button.Classes.Add("accent");
        }

        return button;
    }
}
