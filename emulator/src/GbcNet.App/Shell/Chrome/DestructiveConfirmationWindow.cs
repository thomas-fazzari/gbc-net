// Copyright (C) 2026 GBC.Net Contributors
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
        Content = BuildContent(heading, message, destructiveButtonLabel);
    }

    private static Button CreateButton(string text, bool destructive = false)
    {
        var button = new Button { Content = text };
        button.Classes.Add(destructive ? "destructive" : "secondary");

        return button;
    }

    private StackPanel BuildContent(string heading, string message, string destructiveButtonLabel)
    {
        var cancelButton = CreateButton("Cancel");
        cancelButton.Click += (_, _) => Close(dialogResult: false);

        var destructiveButton = CreateButton(destructiveButtonLabel, destructive: true);
        destructiveButton.Click += (_, _) => Close(dialogResult: true);
        return new StackPanel
        {
            Width = 380,
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = heading, Classes = { "subtitle" } },
                new TextBlock
                {
                    Text = message,
                    Classes = { "body", "muted" },
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
