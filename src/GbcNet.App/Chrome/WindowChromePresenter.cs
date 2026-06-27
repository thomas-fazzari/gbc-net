using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.App.Menus;

namespace GbcNet.App.Chrome;

internal sealed class WindowChromePresenter(Window window, Control statusBar, MainMenu menu)
{
    public void SyncMenuState()
    {
        menu.SetFullscreenState(window.WindowState is WindowState.FullScreen);
        menu.SetStatusBarState(statusBar.IsVisible);
    }

    public void SyncFullscreenState(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == Window.WindowStateProperty)
        {
            menu.SetFullscreenState(window.WindowState is WindowState.FullScreen);
        }
    }

    public void ToggleFullscreen()
    {
        window.WindowState =
            window.WindowState is WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
    }

    public void ToggleStatusBar()
    {
        statusBar.IsVisible = !statusBar.IsVisible;
        menu.SetStatusBarState(statusBar.IsVisible);
    }

    public bool TryHandleShortcut(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.Enter when modifiers.HasFlag(KeyModifiers.Alt):
                ToggleFullscreen();
                return true;

            case Key.I
                when modifiers.HasFlag(
                    OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
                ):
                ToggleStatusBar();
                return true;

            default:
                return false;
        }
    }
}
