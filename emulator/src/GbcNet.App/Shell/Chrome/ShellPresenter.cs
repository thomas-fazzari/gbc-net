// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace GbcNet.App.Shell.Chrome;

internal sealed class ShellPresenter(
    TextBlock romTitle,
    Border emulationStateBadge,
    TextBlock emulationState,
    ToggleButton pauseButton,
    ToggleButton fastForwardButton,
    Border notification,
    ItemsControl notificationItems
)
{
    public void ShowError(string text)
    {
        var messages = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        notificationItems.ItemsSource = messages.Length == 0 ? [text] : messages;
        notification.IsVisible = true;
    }

    public void DismissError() => notification.IsVisible = false;

    public void ShowRomFileName(string romFileName) =>
        romTitle.Text = GetRomDisplayTitle(Path.GetFileNameWithoutExtension(romFileName));

    public void ShowEmulationState(
        bool hasSession,
        bool isPaused,
        bool fastForwardEnabled,
        string effectiveSpeed
    )
    {
        pauseButton.IsChecked = isPaused;
        fastForwardButton.IsChecked = fastForwardEnabled;
        emulationState.Text = isPaused ? "Paused" : effectiveSpeed.Replace('x', '×');
        emulationStateBadge.IsVisible = hasSession;
    }

    private static string GetRomDisplayTitle(string fileName)
    {
        var end = fileName.Length;
        while (end > 0 && fileName[end - 1] == ')')
        {
            var openingParenthesis = fileName.LastIndexOf('(', end - 1);
            if (openingParenthesis <= 0 || !char.IsWhiteSpace(fileName[openingParenthesis - 1]))
            {
                break;
            }

            end = openingParenthesis;
            while (end > 0 && char.IsWhiteSpace(fileName[end - 1]))
            {
                end--;
            }
        }

        return fileName[..end];
    }
}
