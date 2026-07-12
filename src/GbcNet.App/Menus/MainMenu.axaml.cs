// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.App.Emulation;
using GbcNet.App.Library;

namespace GbcNet.App.Menus;

internal sealed partial class MainMenu : UserControl
{
    private static readonly KeyGesture _fullscreenGesture = KeyGesture.Parse("Alt+Enter");
    private static readonly KeyGesture _fastForwardGesture = KeyGesture.Parse("Tab");
    private static readonly KeyGesture _statusBarGesture = KeyGesture.Parse(
        OperatingSystem.IsMacOS() ? "Meta+I" : "Ctrl+I"
    );
    private static readonly KeyGesture _menuBarGesture = KeyGesture.Parse("Ctrl+M");

    private readonly NativeMenuItem _nativePauseMenuItem = new("Pause")
    {
        Gesture = KeyGesture.Parse("Space"),
        IsEnabled = false,
    };

    private readonly NativeMenuItem _nativeResetMenuItem = new("Reset")
    {
        Gesture = KeyGesture.Parse("Meta+R"),
        IsEnabled = false,
    };

    private readonly NativeMenuItem _nativeSaveStateMenuItem = new("Save State")
    {
        IsEnabled = false,
    };

    private readonly NativeMenuItem _nativeLoadStateMenuItem = new("Load State")
    {
        IsEnabled = false,
    };

    private readonly NativeMenuItem _nativeFastForwardMenuItem = new("Fast Forward")
    {
        Gesture = _fastForwardGesture,
        ToggleType = MenuItemToggleType.CheckBox,
    };

    private readonly NativeMenuItem _nativeFullscreenMenuItem = new("Fullscreen")
    {
        Gesture = _fullscreenGesture,
        ToggleType = MenuItemToggleType.CheckBox,
    };

    private readonly NativeMenuItem _nativeStatusBarMenuItem = new("Status Bar")
    {
        Gesture = _statusBarGesture,
        ToggleType = MenuItemToggleType.CheckBox,
        IsChecked = true,
    };

    private readonly List<(
        NativeMenuItem NativeItem,
        MenuItem WindowItem,
        EmulationSpeed Speed
    )> _fastForwardSpeedMenuItems = [];
    private readonly NativeMenu _nativeFastForwardSpeedMenu = [];
    private readonly NativeMenu _nativeOpenRecentMenu = [];
    private readonly NativeMenuItem _nativeOpenRecentMenuItem;
    private readonly NativeMenuItem _nativeCloseMenuItem = new("Close")
    {
        Gesture = KeyGesture.Parse("Meta+W"),
        IsEnabled = false,
    };
    private readonly NativeMenu _nativeMenu;

    public MainMenu()
    {
        InitializeComponent();

        IsVisible = !OperatingSystem.IsMacOS();
        _nativeOpenRecentMenuItem = new NativeMenuItem("Open Recent")
        {
            IsEnabled = false,
            Menu = _nativeOpenRecentMenu,
        };
        ConfigureWindowMenu();
        _nativeMenu = CreateNativeMenu();
    }

    public event EventHandler? OpenRomRequested;

    public event EventHandler? RecentRomsRequested;

    public event EventHandler<RecentRomSelectedEventArgs>? RecentRomSelected;

    public event EventHandler? CloseRequested;

    public event EventHandler? ConfigurationRequested;

    public event EventHandler? ConfigurationFileLocationRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? ResetRequested;

    public event EventHandler? SaveStateRequested;

    public event EventHandler? LoadStateRequested;

    public event EventHandler? FastForwardRequested;

    public event EventHandler<FastForwardSpeedSelectedEventArgs>? FastForwardSpeedSelected;

    public event EventHandler? FullscreenRequested;

    public event EventHandler? MenuBarRequested;

    public event EventHandler? StatusBarRequested;

    public event EventHandler? GitHubRepositoryRequested;

    public void AttachNativeMenu(Window window)
    {
        if (OperatingSystem.IsMacOS())
        {
            NativeMenu.SetMenu(window, _nativeMenu);
        }
    }

    public bool TryHandleShortcut(Key key, KeyModifiers modifiers)
    {
        if (OperatingSystem.IsMacOS() || modifiers is not KeyModifiers.Control)
        {
            return false;
        }

        switch (key)
        {
            case Key.O:
                OpenRomRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.W when CloseWindowMenuItem.IsEnabled:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.C:
                ConfigurationRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.M:
                MenuBarRequested?.Invoke(this, EventArgs.Empty);
                return true;

            case Key.R when ResetEmulationMenuItem.IsEnabled:
                ResetRequested?.Invoke(this, EventArgs.Empty);
                return true;

            default:
                return false;
        }
    }

    public void SetEmulationActionsEnabled(bool isEnabled)
    {
        SetPauseState(isEnabled, isPaused: false);
        SetEnabled(_nativeResetMenuItem, ResetEmulationMenuItem, isEnabled);
        SetEnabled(_nativeSaveStateMenuItem, SaveStateMenuItem, isEnabled);
        SetEnabled(_nativeLoadStateMenuItem, LoadStateMenuItem, isEnabled);

        SetEnabled(_nativeCloseMenuItem, CloseWindowMenuItem, isEnabled);
    }

    public void SetPauseState(bool isEnabled, bool isPaused)
    {
        var header = isPaused ? "Resume" : "Pause";

        SetHeader(_nativePauseMenuItem, PauseEmulationMenuItem, header);
        SetEnabled(_nativePauseMenuItem, PauseEmulationMenuItem, isEnabled);
    }

    public void SetFastForwardState(bool isEnabled, EmulationSpeed speed)
    {
        SetChecked(_nativeFastForwardMenuItem, FastForwardMenuItem, isEnabled);

        foreach (var (nativeItem, windowItem, itemSpeed) in _fastForwardSpeedMenuItems)
        {
            SetChecked(nativeItem, windowItem, itemSpeed == speed);
        }
    }

    public void SetFullscreenState(bool isFullscreen) =>
        SetChecked(_nativeFullscreenMenuItem, FullscreenMenuItem, isFullscreen);

    public void SetMenuBarState(bool isVisible)
    {
        MenuBarMenuItem.IsChecked = isVisible;
    }

    public void SetStatusBarState(bool isVisible) =>
        SetChecked(_nativeStatusBarMenuItem, StatusBarMenuItem, isVisible);

    public void SetStatusBarAvailability(bool isAvailable) =>
        SetEnabled(_nativeStatusBarMenuItem, StatusBarMenuItem, isAvailable);

    private static void SetChecked(NativeMenuItem nativeItem, MenuItem windowItem, bool isChecked)
    {
        nativeItem.IsChecked = isChecked;
        windowItem.IsChecked = isChecked;
    }

    private static void SetEnabled(NativeMenuItem nativeItem, MenuItem windowItem, bool isEnabled)
    {
        nativeItem.IsEnabled = isEnabled;
        windowItem.IsEnabled = isEnabled;
    }

    private static void SetHeader(NativeMenuItem nativeItem, MenuItem windowItem, string header)
    {
        nativeItem.Header = header;
        windowItem.Header = header;
    }

    public void SetRecentRoms(IReadOnlyList<LibraryEntry> entries)
    {
        OpenRecentMenuItem.Items.Clear();
        _nativeOpenRecentMenu.Items.Clear();

        var hasEntries = entries.Count > 0;
        OpenRecentMenuItem.IsEnabled = hasEntries;
        _nativeOpenRecentMenuItem.IsEnabled = hasEntries;

        foreach (var entry in entries)
        {
            OpenRecentMenuItem.Items.Add(CreateWindowRecentRomMenuItem(entry));
            _nativeOpenRecentMenu.Add(CreateNativeRecentRomMenuItem(entry));
        }
    }

    #region Window menu
    private void ConfigureWindowMenu()
    {
        ConfigureWindowFileMenu();
        ConfigureWindowSettingsMenu();
        ConfigureWindowEmulationMenu();
        ConfigureWindowViewMenu();
        ConfigureWindowHelpMenu();
    }

    private void ConfigureWindowFileMenu()
    {
        FileMenuItem.SubmenuOpened += (_, _) => RecentRomsRequested?.Invoke(this, EventArgs.Empty);
        OpenRomMenuItem.Click += (_, _) => OpenRomRequested?.Invoke(this, EventArgs.Empty);
        CloseWindowMenuItem.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureWindowSettingsMenu()
    {
        ConfigurationMenuItem.Click += (_, _) =>
            ConfigurationRequested?.Invoke(this, EventArgs.Empty);
        ConfigurationFileLocationMenuItem.Click += (_, _) =>
            ConfigurationFileLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureWindowEmulationMenu()
    {
        PauseEmulationMenuItem.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        ResetEmulationMenuItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        SaveStateMenuItem.Click += (_, _) => SaveStateRequested?.Invoke(this, EventArgs.Empty);
        LoadStateMenuItem.Click += (_, _) => LoadStateRequested?.Invoke(this, EventArgs.Empty);

        FastForwardMenuItem.InputGesture = _fastForwardGesture;
        FastForwardMenuItem.Click += (_, _) => FastForwardRequested?.Invoke(this, EventArgs.Empty);
        ConfigureFastForwardSpeedMenuItems();
    }

    private void ConfigureWindowViewMenu()
    {
        FullscreenMenuItem.InputGesture = _fullscreenGesture;
        FullscreenMenuItem.Click += (_, _) => FullscreenRequested?.Invoke(this, EventArgs.Empty);
        MenuBarMenuItem.InputGesture = _menuBarGesture;
        MenuBarMenuItem.Click += (_, _) => MenuBarRequested?.Invoke(this, EventArgs.Empty);
        StatusBarMenuItem.InputGesture = _statusBarGesture;
        StatusBarMenuItem.Click += (_, _) => StatusBarRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureWindowHelpMenu()
    {
        GitHubRepositoryMenuItem.Click += (_, _) =>
            GitHubRepositoryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureFastForwardSpeedMenuItems()
    {
        foreach (var speed in Enum.GetValues<EmulationSpeed>())
        {
            var windowItem = CreateWindowFastForwardSpeedMenuItem(speed);
            var nativeItem = CreateNativeFastForwardSpeedMenuItem(speed);
            _fastForwardSpeedMenuItems.Add((nativeItem, windowItem, speed));
            FastForwardSpeedMenuItem.Items.Add(windowItem);
            _nativeFastForwardSpeedMenu.Add(nativeItem);
        }
    }

    private MenuItem CreateWindowFastForwardSpeedMenuItem(EmulationSpeed speed)
    {
        var item = new MenuItem
        {
            Header = speed.GetDisplayName(),
            ToggleType = MenuItemToggleType.CheckBox,
        };
        item.Click += (_, _) =>
            FastForwardSpeedSelected?.Invoke(this, new FastForwardSpeedSelectedEventArgs(speed));
        return item;
    }

    private MenuItem CreateWindowRecentRomMenuItem(LibraryEntry entry)
    {
        var item = new MenuItem { Header = entry.FileName };
        item.Click += (_, _) =>
            RecentRomSelected?.Invoke(this, new RecentRomSelectedEventArgs(entry.LastKnownPath));
        return item;
    }

    #endregion Window menu

    #region Native menu
    private NativeMenu CreateNativeMenu() =>
        [
            new NativeMenuItem("File") { Menu = CreateNativeFileMenu() },
            new NativeMenuItem("Emulation") { Menu = CreateNativeEmulationMenu() },
            new NativeMenuItem("Settings") { Menu = CreateNativeSettingsMenu() },
            new NativeMenuItem("View") { Menu = CreateNativeViewMenu() },
            new NativeMenuItem("Help") { Menu = CreateNativeHelpMenu() },
        ];

    private NativeMenu CreateNativeFileMenu()
    {
        var openItem = new NativeMenuItem("Open ROM...") { Gesture = KeyGesture.Parse("Meta+O") };
        openItem.Click += (_, _) => OpenRomRequested?.Invoke(this, EventArgs.Empty);

        var fileMenu = new NativeMenu
        {
            openItem,
            _nativeOpenRecentMenuItem,
            new NativeMenuItemSeparator(),
        };
        fileMenu.NeedsUpdate += (_, _) => RecentRomsRequested?.Invoke(this, EventArgs.Empty);

        _nativeCloseMenuItem.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        fileMenu.Add(_nativeCloseMenuItem);

        return fileMenu;
    }

    private NativeMenu CreateNativeSettingsMenu()
    {
        var configurationItem = new NativeMenuItem("Configuration")
        {
            Gesture = KeyGesture.Parse("Meta+C"),
        };
        configurationItem.Click += (_, _) => ConfigurationRequested?.Invoke(this, EventArgs.Empty);
        var fileLocationItem = new NativeMenuItem("Open Config File Location");
        fileLocationItem.Click += (_, _) =>
            ConfigurationFileLocationRequested?.Invoke(this, EventArgs.Empty);

        return [configurationItem, new NativeMenuItemSeparator(), fileLocationItem];
    }

    private NativeMenu CreateNativeEmulationMenu()
    {
        _nativePauseMenuItem.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        _nativeResetMenuItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        _nativeSaveStateMenuItem.Click += (_, _) =>
            SaveStateRequested?.Invoke(this, EventArgs.Empty);
        _nativeLoadStateMenuItem.Click += (_, _) =>
            LoadStateRequested?.Invoke(this, EventArgs.Empty);

        _nativeFastForwardMenuItem.Click += (_, _) =>
            FastForwardRequested?.Invoke(this, EventArgs.Empty);

        return
        [
            _nativePauseMenuItem,
            _nativeResetMenuItem,
            new NativeMenuItemSeparator(),
            _nativeSaveStateMenuItem,
            _nativeLoadStateMenuItem,
            new NativeMenuItemSeparator(),
            _nativeFastForwardMenuItem,
            new NativeMenuItem("Fast Forward Speed") { Menu = _nativeFastForwardSpeedMenu },
        ];
    }

    private NativeMenu CreateNativeViewMenu()
    {
        _nativeFullscreenMenuItem.Click += (_, _) =>
            FullscreenRequested?.Invoke(this, EventArgs.Empty);
        _nativeStatusBarMenuItem.Click += (_, _) =>
            StatusBarRequested?.Invoke(this, EventArgs.Empty);

        return [_nativeFullscreenMenuItem, new NativeMenuItemSeparator(), _nativeStatusBarMenuItem];
    }

    private NativeMenu CreateNativeHelpMenu()
    {
        var item = new NativeMenuItem("View on GitHub");
        item.Click += (_, _) => GitHubRepositoryRequested?.Invoke(this, EventArgs.Empty);
        return [item];
    }

    private NativeMenuItem CreateNativeFastForwardSpeedMenuItem(EmulationSpeed speed)
    {
        NativeMenuItem item = new(speed.GetDisplayName())
        {
            ToggleType = MenuItemToggleType.CheckBox,
        };
        item.Click += (_, _) =>
            FastForwardSpeedSelected?.Invoke(this, new FastForwardSpeedSelectedEventArgs(speed));
        return item;
    }

    private NativeMenuItem CreateNativeRecentRomMenuItem(LibraryEntry entry)
    {
        NativeMenuItem item = new(entry.FileName);
        item.Click += (_, _) =>
            RecentRomSelected?.Invoke(this, new RecentRomSelectedEventArgs(entry.LastKnownPath));
        return item;
    }
    #endregion Native menu

    internal sealed class RecentRomSelectedEventArgs(string path) : EventArgs
    {
        public string Path { get; } = path;
    }

    internal sealed class FastForwardSpeedSelectedEventArgs(EmulationSpeed speed) : EventArgs
    {
        public EmulationSpeed Speed { get; } = speed;
    }
}
