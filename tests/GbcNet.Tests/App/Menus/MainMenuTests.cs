// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using GbcNet.App.Menus;

namespace GbcNet.Tests.App.Menus;

public sealed class MainMenuTests
{
    [Fact]
    public void SaveStateActions_FollowSessionAvailabilityAndRaiseRequests()
    {
        var menu = new MainMenu();
        var saveStateMenuItem = menu.FindControl<MenuItem>("SaveStateMenuItem")!;
        var loadStateMenuItem = menu.FindControl<MenuItem>("LoadStateMenuItem")!;
        var saveRequested = false;
        var loadRequested = false;
        menu.SaveStateRequested += (_, _) => saveRequested = true;
        menu.LoadStateRequested += (_, _) => loadRequested = true;

        Assert.False(saveStateMenuItem.IsEnabled);
        Assert.False(loadStateMenuItem.IsEnabled);

        menu.SetEmulationActionsEnabled(isEnabled: true);

        Assert.True(saveStateMenuItem.IsEnabled);
        Assert.True(loadStateMenuItem.IsEnabled);
        saveStateMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        loadStateMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.True(saveRequested);
        Assert.True(loadRequested);

        menu.SetEmulationActionsEnabled(isEnabled: false);

        Assert.False(saveStateMenuItem.IsEnabled);
        Assert.False(loadStateMenuItem.IsEnabled);
    }
}
