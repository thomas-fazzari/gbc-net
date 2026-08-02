// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GbcNet.App.Shell.Chrome;

internal sealed class DestructiveConfirmationWindow : Window
{
    public DestructiveConfirmationWindow(
        string title,
        string heading,
        string message,
        string destructiveButtonLabel
    )
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = AppChrome.Brush(AppChrome.Bg);
        Content = BuildContent(heading, message, destructiveButtonLabel);
    }

    private StackPanel BuildContent(string heading, string message, string destructiveButtonLabel)
    {
        var cancelButton = AppChrome.Button("Cancel");
        cancelButton.Click += (_, _) => Close(dialogResult: false);

        var destructiveButton = AppChrome.Button(destructiveButtonLabel, accent: true);
        destructiveButton.Click += (_, _) => Close(dialogResult: true);

        return new StackPanel
        {
            Width = 360,
            Margin = new Thickness(18),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = heading,
                    Foreground = AppChrome.Brush(AppChrome.Text),
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                },
                new TextBlock
                {
                    Text = message,
                    Foreground = AppChrome.Brush(AppChrome.Muted),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, destructiveButton },
                },
            },
        };
    }
}
