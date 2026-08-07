// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Menus;

namespace GbcNet.App.Shell;

/// <summary>
/// Owns the status-bar visibility state machine: whether the bar is available
/// for the current view and whether the user has chosen to show it.
/// </summary>
internal sealed class StatusBarVisibilityController(Border statusBar, MainMenu mainMenu)
{
    private bool _available = true;
    private bool _visibleWhenAvailable = true;

    public bool IsVisible => statusBar.IsVisible;

    public void SetAvailable(bool isAvailable)
    {
        _available = isAvailable;
        Apply();
    }

    public void SetVisible(bool isVisible)
    {
        _visibleWhenAvailable = isVisible;
        Apply();
    }

    public void Toggle()
    {
        if (!_available)
        {
            return;
        }

        _visibleWhenAvailable = !_visibleWhenAvailable;
        Apply();
    }

    public void Apply()
    {
        statusBar.IsVisible = _available && _visibleWhenAvailable;
        mainMenu.SetStatusBarAvailability(isAvailable: _available);
        mainMenu.SetStatusBarState(isVisible: statusBar.IsVisible);
    }
}
