// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.App.Emulation;
using GbcNet.App.Library;

namespace GbcNet.App.Menus;

internal sealed partial class MainMenu : UserControl
{
    private static readonly KeyGesture _fullscreenGesture = KeyGesture.Parse(gesture: "Alt+Enter");
    private static readonly KeyGesture _fastForwardGesture = KeyGesture.Parse(gesture: "Tab");
    private static readonly KeyGesture _muteGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+Shift+M" : "Ctrl+Shift+M"
    );
    private static readonly KeyGesture _statusBarGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+I" : "Ctrl+I"
    );
    private static readonly KeyGesture _menuBarGesture = KeyGesture.Parse(gesture: "Ctrl+M");
    private const int StateSlotCount = 10;

    private readonly MenuItem[] _saveStateSlotMenuItems = new MenuItem[StateSlotCount];
    private readonly MenuItem[] _loadStateSlotMenuItems = new MenuItem[StateSlotCount];
    private readonly NativeMenuItem[] _nativeSaveStateSlotMenuItems = new NativeMenuItem[
        StateSlotCount
    ];
    private readonly NativeMenuItem[] _nativeLoadStateSlotMenuItems = new NativeMenuItem[
        StateSlotCount
    ];
    private readonly List<MenuCommand> _commands = [];
    private readonly List<(
        NativeMenuItem NativeItem,
        MenuItem WindowItem,
        EmulationSpeed Speed
    )> _fastForwardSpeedMenuItems = [];
    private readonly NativeMenu _nativeSaveStateMenu = [];
    private readonly NativeMenu _nativeLoadStateMenu = [];
    private readonly NativeMenu _nativeFastForwardSpeedMenu = [];
    private readonly NativeMenu _nativeOpenRecentMenu = [];
    private readonly NativeMenuItem _nativePauseMenuItem;
    private readonly NativeMenuItem _nativeResetMenuItem;
    private readonly NativeMenuItem _nativeCheatsMenuItem;
    private readonly NativeMenuItem _nativeSaveStateMenuItem;
    private readonly NativeMenuItem _nativeLoadStateMenuItem;
    private readonly NativeMenuItem _nativeFastForwardMenuItem;
    private readonly NativeMenuItem _nativeMuteAudioMenuItem;
    private readonly NativeMenuItem _nativeFullscreenMenuItem;
    private readonly NativeMenuItem _nativeStatusBarMenuItem;
    private readonly NativeMenuItem _nativeOpenRecentMenuItem;
    private readonly NativeMenuItem _nativeCloseMenuItem;
    private readonly NativeMenu _nativeMenu;
    private bool _emulationActionsEnabled;
    private bool _pauseEnabled;
    private bool _cheatsEnabled;
    private bool _statusBarAvailable = true;

    public MainMenu()
    {
        InitializeComponent();

        OpenRomCommand = CreateCommand(_ => OpenRom?.Invoke());
        CloseCommand = CreateCommand(_ => Close?.Invoke(), () => _emulationActionsEnabled);
        ConfigurationCommand = CreateCommand(_ => OpenConfiguration?.Invoke());
        ConfigurationFileLocationCommand = CreateCommand(_ =>
            OpenConfigurationFileLocation?.Invoke()
        );
        LogFileLocationCommand = CreateCommand(_ => OpenLogFileLocation?.Invoke());
        PauseCommand = CreateCommand(_ => TogglePause?.Invoke(), () => _pauseEnabled);
        ResetCommand = CreateCommand(_ => Reset?.Invoke(), () => _emulationActionsEnabled);
        CheatsCommand = CreateCommand(_ => OpenCheats?.Invoke(), () => _cheatsEnabled);
        MuteCommand = CreateCommand(_ => ToggleMute?.Invoke());
        SaveStateCommand = CreateCommand(
            parameter =>
            {
                if (parameter is int slotIndex)
                {
                    SaveState?.Invoke(slotIndex);
                }
            },
            () => _emulationActionsEnabled
        );
        LoadStateCommand = CreateCommand(
            parameter =>
            {
                if (parameter is int slotIndex)
                {
                    LoadState?.Invoke(slotIndex);
                }
            },
            () => _emulationActionsEnabled
        );
        FastForwardCommand = CreateCommand(_ => ToggleFastForward?.Invoke());
        FastForwardSpeedCommand = CreateCommand(parameter =>
        {
            if (parameter is EmulationSpeed speed)
            {
                SetFastForwardSpeed?.Invoke(speed);
            }
        });
        OpenRecentRomCommand = CreateCommand(parameter =>
        {
            if (parameter is string path)
            {
                OpenRecentRom?.Invoke(path);
            }
        });
        FullscreenCommand = CreateCommand(_ => ToggleFullscreen?.Invoke());
        MenuBarCommand = CreateCommand(_ => ToggleMenuBar?.Invoke());
        StatusBarCommand = CreateCommand(_ => ToggleStatusBar?.Invoke(), () => _statusBarAvailable);
        GitHubRepositoryCommand = CreateCommand(_ => OpenGitHubRepository?.Invoke());

        IsVisible = !OperatingSystem.IsMacOS();
        _nativePauseMenuItem = new NativeMenuItem(header: "Pause")
        {
            Command = PauseCommand,
            Gesture = KeyGesture.Parse(gesture: "Space"),
        };
        _nativeResetMenuItem = new NativeMenuItem(header: "Reset")
        {
            Command = ResetCommand,
            Gesture = KeyGesture.Parse(gesture: "Meta+R"),
        };
        _nativeCheatsMenuItem = new NativeMenuItem(header: "Cheats...")
        {
            Command = CheatsCommand,
            Gesture = KeyGesture.Parse(gesture: "Meta+G"),
        };
        _nativeOpenRecentMenuItem = new NativeMenuItem(header: "Open Recent")
        {
            IsEnabled = false,
            Menu = _nativeOpenRecentMenu,
        };
        _nativeSaveStateMenuItem = new NativeMenuItem(header: "Save State")
        {
            IsEnabled = false,
            Menu = _nativeSaveStateMenu,
        };
        _nativeLoadStateMenuItem = new NativeMenuItem(header: "Load State")
        {
            IsEnabled = false,
            Menu = _nativeLoadStateMenu,
        };
        _nativeFastForwardMenuItem = new NativeMenuItem(header: "Fast Forward")
        {
            Command = FastForwardCommand,
            Gesture = _fastForwardGesture,
            ToggleType = MenuItemToggleType.CheckBox,
        };
        _nativeMuteAudioMenuItem = new NativeMenuItem(header: "Mute Audio")
        {
            Command = MuteCommand,
            Gesture = _muteGesture,
            ToggleType = MenuItemToggleType.CheckBox,
        };
        _nativeFullscreenMenuItem = new NativeMenuItem(header: "Fullscreen")
        {
            Command = FullscreenCommand,
            Gesture = _fullscreenGesture,
            ToggleType = MenuItemToggleType.CheckBox,
        };
        _nativeStatusBarMenuItem = new NativeMenuItem(header: "Status Bar")
        {
            Command = StatusBarCommand,
            Gesture = _statusBarGesture,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _nativeCloseMenuItem = new NativeMenuItem(header: "Close")
        {
            Command = CloseCommand,
            Gesture = KeyGesture.Parse(gesture: "Meta+W"),
        };

        ConfigureWindowMenu();
        _nativeMenu = CreateNativeMenu();
    }

    public Action? OpenRom { get; set; }

    public Action? RefreshRecentRoms { get; set; }

    public Action<string>? OpenRecentRom { get; set; }

    public Action? Close { get; set; }

    public Action? OpenConfiguration { get; set; }

    public Action? OpenConfigurationFileLocation { get; set; }

    public Action? OpenLogFileLocation { get; set; }

    public Action? TogglePause { get; set; }

    public Action? Reset { get; set; }

    public Action? OpenCheats { get; set; }

    public Action<int>? SaveState { get; set; }

    public Action<int>? LoadState { get; set; }

    public Action? ToggleFastForward { get; set; }

    public Action<EmulationSpeed>? SetFastForwardSpeed { get; set; }

    public Action? ToggleMute { get; set; }

    public Action? ToggleFullscreen { get; set; }

    public Action? ToggleMenuBar { get; set; }

    public Action? ToggleStatusBar { get; set; }

    public Action? OpenGitHubRepository { get; set; }

    public ICommand OpenRomCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand ConfigurationCommand { get; }

    public ICommand ConfigurationFileLocationCommand { get; }

    public ICommand LogFileLocationCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand CheatsCommand { get; }

    public ICommand MuteCommand { get; }

    public ICommand SaveStateCommand { get; }

    public ICommand LoadStateCommand { get; }

    public ICommand FastForwardCommand { get; }

    public ICommand FastForwardSpeedCommand { get; }

    public ICommand OpenRecentRomCommand { get; }

    public ICommand FullscreenCommand { get; }

    public ICommand MenuBarCommand { get; }

    public ICommand StatusBarCommand { get; }

    public ICommand GitHubRepositoryCommand { get; }

    public void AttachNativeMenu(Window window)
    {
        ConfigureKeyBindings(window);

        if (OperatingSystem.IsMacOS())
        {
            NativeMenu.SetMenu(o: window, menu: _nativeMenu);
        }
    }

    public void SetEmulationActionsEnabled(bool isEnabled)
    {
        _emulationActionsEnabled = isEnabled;
        SaveStateMenuItem.IsEnabled = isEnabled;
        _nativeSaveStateMenuItem.IsEnabled = isEnabled;
        LoadStateMenuItem.IsEnabled = isEnabled;
        _nativeLoadStateMenuItem.IsEnabled = isEnabled;
        SetPauseState(isEnabled: isEnabled, isPaused: false);
        RefreshCommandStates();
    }

    public void SetCheatsEnabled(bool enabled)
    {
        _cheatsEnabled = enabled;
        RefreshCommandStates();
    }

    public void SetSaveStateDates(IReadOnlyList<DateTime?> dates)
    {
        for (var slotIndex = 0; slotIndex < StateSlotCount; slotIndex++)
        {
            var header = dates[index: slotIndex] is { } date
                ? $"Slot {slotIndex + 1} — {date:g}"
                : $"Slot {slotIndex + 1}";
            _saveStateSlotMenuItems[slotIndex].Header = header;
            _loadStateSlotMenuItems[slotIndex].Header = header;
            _nativeSaveStateSlotMenuItems[slotIndex].Header = header;
            _nativeLoadStateSlotMenuItems[slotIndex].Header = header;
        }
    }

    public void SetPauseState(bool isEnabled, bool isPaused)
    {
        _pauseEnabled = isEnabled;
        var header = isPaused ? "Resume" : "Pause";
        _nativePauseMenuItem.Header = header;
        PauseEmulationMenuItem.Header = header;
        RefreshCommandStates();
    }

    public void SetFastForwardState(bool isEnabled, EmulationSpeed speed)
    {
        SetChecked(
            nativeItem: _nativeFastForwardMenuItem,
            windowItem: FastForwardMenuItem,
            isChecked: isEnabled
        );

        foreach (var (nativeItem, windowItem, itemSpeed) in _fastForwardSpeedMenuItems)
        {
            SetChecked(nativeItem, windowItem, itemSpeed == speed);
        }
    }

    public void SetMuteState(bool isMuted) =>
        SetChecked(_nativeMuteAudioMenuItem, MuteAudioMenuItem, isMuted);

    public void SetFullscreenState(bool isFullscreen) =>
        SetChecked(_nativeFullscreenMenuItem, FullscreenMenuItem, isFullscreen);

    public void SetMenuBarState(bool isVisible) => MenuBarMenuItem.IsChecked = isVisible;

    public void SetStatusBarState(bool isVisible) =>
        SetChecked(_nativeStatusBarMenuItem, StatusBarMenuItem, isVisible);

    public void SetStatusBarAvailability(bool isAvailable)
    {
        _statusBarAvailable = isAvailable;
        RefreshCommandStates();
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

    private MenuCommand CreateCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        var command = new MenuCommand(execute, canExecute);
        _commands.Add(command);
        return command;
    }

    private void RefreshCommandStates()
    {
        foreach (var command in _commands)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private static void SetChecked(NativeMenuItem nativeItem, MenuItem windowItem, bool isChecked)
    {
        nativeItem.IsChecked = isChecked;
        windowItem.IsChecked = isChecked;
    }

    private void ConfigureWindowMenu()
    {
        FileMenuItem.SubmenuOpened += (_, _) => RefreshRecentRoms?.Invoke();
        MuteAudioMenuItem.InputGesture = _muteGesture;
        FastForwardMenuItem.InputGesture = _fastForwardGesture;
        FullscreenMenuItem.InputGesture = _fullscreenGesture;
        MenuBarMenuItem.InputGesture = _menuBarGesture;
        StatusBarMenuItem.InputGesture = _statusBarGesture;
        ConfigureStateSlotMenuItems();
        ConfigureFastForwardSpeedMenuItems();
    }

    private void ConfigureKeyBindings(Window window)
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        AddKeyBinding(window, "Ctrl+O", OpenRomCommand);
        AddKeyBinding(window, "Ctrl+W", CloseCommand);
        AddKeyBinding(window, "Ctrl+C", ConfigurationCommand);
        AddKeyBinding(window, "Space", PauseCommand);
        AddKeyBinding(window, "Ctrl+R", ResetCommand);
        AddKeyBinding(window, "Ctrl+G", CheatsCommand);
        AddKeyBinding(window, "Ctrl+Shift+M", MuteCommand);
        AddKeyBinding(window, "Tab", FastForwardCommand);
        AddKeyBinding(window, "Alt+Enter", FullscreenCommand);
        AddKeyBinding(window, "Ctrl+M", MenuBarCommand);
        AddKeyBinding(window, "Ctrl+I", StatusBarCommand);
    }

    private static void AddKeyBinding(Window window, string gesture, ICommand command) =>
        window.KeyBindings.Add(
            new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = command }
        );

    private void ConfigureStateSlotMenuItems()
    {
        for (var slotIndex = 0; slotIndex < StateSlotCount; slotIndex++)
        {
            var saveItem = CreateWindowStateSlotMenuItem(slotIndex, SaveStateCommand);
            var loadItem = CreateWindowStateSlotMenuItem(slotIndex, LoadStateCommand);
            var nativeSaveItem = CreateNativeStateSlotMenuItem(slotIndex, SaveStateCommand);
            var nativeLoadItem = CreateNativeStateSlotMenuItem(slotIndex, LoadStateCommand);

            _saveStateSlotMenuItems[slotIndex] = saveItem;
            _loadStateSlotMenuItems[slotIndex] = loadItem;
            _nativeSaveStateSlotMenuItems[slotIndex] = nativeSaveItem;
            _nativeLoadStateSlotMenuItems[slotIndex] = nativeLoadItem;
            SaveStateMenuItem.Items.Add(saveItem);
            LoadStateMenuItem.Items.Add(loadItem);
            _nativeSaveStateMenu.Add(nativeSaveItem);
            _nativeLoadStateMenu.Add(nativeLoadItem);
        }
    }

    private static MenuItem CreateWindowStateSlotMenuItem(int slotIndex, ICommand command) =>
        new()
        {
            Header = $"Slot {slotIndex + 1}",
            Command = command,
            CommandParameter = slotIndex,
        };

    private static NativeMenuItem CreateNativeStateSlotMenuItem(int slotIndex, ICommand command) =>
        new(header: $"Slot {slotIndex + 1}") { Command = command, CommandParameter = slotIndex };

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

    private MenuItem CreateWindowFastForwardSpeedMenuItem(EmulationSpeed speed) =>
        new()
        {
            Header = speed.GetDisplayName(),
            ToggleType = MenuItemToggleType.CheckBox,
            Command = FastForwardSpeedCommand,
            CommandParameter = speed,
        };

    private MenuItem CreateWindowRecentRomMenuItem(LibraryEntry entry) =>
        new()
        {
            Header = entry.FileName,
            Command = OpenRecentRomCommand,
            CommandParameter = entry.LastKnownPath,
        };

    private NativeMenu CreateNativeMenu() =>
        [
            new NativeMenuItem(header: "File") { Menu = CreateNativeFileMenu() },
            new NativeMenuItem(header: "Emulation") { Menu = CreateNativeEmulationMenu() },
            new NativeMenuItem(header: "Settings") { Menu = CreateNativeSettingsMenu() },
            new NativeMenuItem(header: "View") { Menu = CreateNativeViewMenu() },
            new NativeMenuItem(header: "Help") { Menu = CreateNativeHelpMenu() },
        ];

    private NativeMenu CreateNativeFileMenu()
    {
        var fileMenu = new NativeMenu
        {
            new NativeMenuItem(header: "Open ROM...")
            {
                Command = OpenRomCommand,
                Gesture = KeyGesture.Parse(gesture: "Meta+O"),
            },
            _nativeOpenRecentMenuItem,
            new NativeMenuItemSeparator(),
            _nativeCloseMenuItem,
        };
        fileMenu.NeedsUpdate += (_, _) => RefreshRecentRoms?.Invoke();
        return fileMenu;
    }

    private NativeMenu CreateNativeSettingsMenu() =>
        [
            new NativeMenuItem(header: "Configuration")
            {
                Command = ConfigurationCommand,
                Gesture = KeyGesture.Parse(gesture: "Meta+C"),
            },
            new NativeMenuItemSeparator(),
            new NativeMenuItem(header: "Open Config File Location")
            {
                Command = ConfigurationFileLocationCommand,
            },
            new NativeMenuItem(header: "Open Logs Folder") { Command = LogFileLocationCommand },
        ];

    private NativeMenu CreateNativeEmulationMenu() =>
        [
            _nativePauseMenuItem,
            _nativeResetMenuItem,
            _nativeCheatsMenuItem,
            _nativeMuteAudioMenuItem,
            new NativeMenuItemSeparator(),
            _nativeSaveStateMenuItem,
            _nativeLoadStateMenuItem,
            new NativeMenuItemSeparator(),
            _nativeFastForwardMenuItem,
            new NativeMenuItem(header: "Fast Forward Speed") { Menu = _nativeFastForwardSpeedMenu },
        ];

    private NativeMenu CreateNativeViewMenu() =>
        [_nativeFullscreenMenuItem, new NativeMenuItemSeparator(), _nativeStatusBarMenuItem];

    private NativeMenu CreateNativeHelpMenu() =>
        [new NativeMenuItem(header: "View on GitHub") { Command = GitHubRepositoryCommand }];

    private NativeMenuItem CreateNativeFastForwardSpeedMenuItem(EmulationSpeed speed) =>
        new(header: speed.GetDisplayName())
        {
            ToggleType = MenuItemToggleType.CheckBox,
            Command = FastForwardSpeedCommand,
            CommandParameter = speed,
        };

    private NativeMenuItem CreateNativeRecentRomMenuItem(LibraryEntry entry) =>
        new(header: entry.FileName)
        {
            Command = OpenRecentRomCommand,
            CommandParameter = entry.LastKnownPath,
        };

    private sealed class MenuCommand(Action<object?> execute, Func<bool>? canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
