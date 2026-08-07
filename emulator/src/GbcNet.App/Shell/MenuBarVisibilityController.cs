// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Menus;

namespace GbcNet.App.Shell;

/// <summary>
/// Owns the menu-bar visibility state machine. On macOS the native menu bar is
/// always present, so visibility is a no-op there.
/// </summary>
internal sealed class MenuBarVisibilityController(MainMenu mainMenu, Window window)
{
    private bool _visibleWhenAvailable = true;

    public void SetVisible(bool isVisible)
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        _visibleWhenAvailable = isVisible;
        Apply();
    }

    public void Toggle()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        _visibleWhenAvailable = !_visibleWhenAvailable;
        Apply();
    }

    public void Apply()
    {
        mainMenu.IsVisible =
            !OperatingSystem.IsMacOS()
            && _visibleWhenAvailable
            && window.WindowState is not WindowState.FullScreen;
        mainMenu.SetMenuBarState(isVisible: _visibleWhenAvailable);
    }
}
