// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GbcNet.App.Emulation;
using GbcNet.App.Library;

namespace GbcNet.App.Menus;

internal sealed partial class MainMenu : UserControl
{
    private static readonly KeyGesture _openRomGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+O" : "Ctrl+O"
    );
    private static readonly KeyGesture _closeGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+W" : "Ctrl+W"
    );
    private static readonly KeyGesture _configurationGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+C" : "Ctrl+C"
    );
    private static readonly KeyGesture _resetGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+R" : "Ctrl+R"
    );
    private static readonly KeyGesture _cheatsGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+G" : "Ctrl+G"
    );
    private static readonly KeyGesture _fullscreenGesture = KeyGesture.Parse(gesture: "Alt+Enter");
    private static readonly KeyGesture _fastForwardGesture = KeyGesture.Parse(gesture: "Tab");
    private static readonly KeyGesture _pauseGesture = KeyGesture.Parse(gesture: "Space");
    private static readonly KeyGesture _muteGesture = KeyGesture.Parse(
        gesture: OperatingSystem.IsMacOS() ? "Meta+Shift+M" : "Ctrl+Shift+M"
    );
    private const int StateSlotCount = 10;

    private readonly MenuItem[] _saveStateSlotMenuItems = new MenuItem[StateSlotCount];
    private readonly MenuItem[] _loadStateSlotMenuItems = new MenuItem[StateSlotCount];
    private readonly List<MenuCommand> _commands = [];
    private readonly List<(MenuItem Item, EmulationSpeed Speed)> _fastForwardSpeedMenuItems = [];
    private bool _emulationActionsEnabled;
    private bool _pauseEnabled;
    private bool _cheatsEnabled;

    public MainMenu()
    {
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
        FullscreenCommand = CreateCommand(_ => RequestFullscreenToggle());
        GitHubRepositoryCommand = CreateCommand(_ => OpenGitHubRepository?.Invoke());

        InitializeComponent();

        ConfigureWindowMenu();
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

    public ICommand GitHubRepositoryCommand { get; }

    public void AttachToWindow(Window window) => ConfigureKeyBindings(window);

    public void SetEmulationActionsEnabled(bool isEnabled)
    {
        _emulationActionsEnabled = isEnabled;
        SaveStateMenuItem.IsEnabled = isEnabled;
        LoadStateMenuItem.IsEnabled = isEnabled;
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
            var date = dates[index: slotIndex];
            var header = date is not null
                ? $"Slot {slotIndex + 1} — {date:g}"
                : $"Slot {slotIndex + 1}";
            _saveStateSlotMenuItems[slotIndex].Header = header;
            _loadStateSlotMenuItems[slotIndex].Header = header;
            _loadStateSlotMenuItems[slotIndex].IsEnabled = date is not null;
        }

        LoadStateMenuItem.IsEnabled =
            _emulationActionsEnabled && dates.Any(date => date is not null);
    }

    public void SetPauseState(bool isEnabled, bool isPaused)
    {
        _pauseEnabled = isEnabled;
        PauseEmulationMenuItem.Header = isPaused ? "Resume" : "Pause";
        RefreshCommandStates();
    }

    public void SetFastForwardState(bool isEnabled, EmulationSpeed speed)
    {
        FastForwardMenuItem.IsChecked = isEnabled;

        foreach (var (item, itemSpeed) in _fastForwardSpeedMenuItems)
        {
            item.IsChecked = itemSpeed == speed;
        }
    }

    public void SetMuteState(bool isMuted) => MuteAudioMenuItem.IsChecked = isMuted;

    public void SetFullscreenState(bool isFullscreen) =>
        FullscreenMenuItem.IsChecked = isFullscreen;

    public void SetRecentRoms(IReadOnlyList<LibraryEntry> entries)
    {
        OpenRecentMenuItem.Items.Clear();

        OpenRecentMenuItem.IsEnabled = entries.Count > 0;

        foreach (var entry in entries)
        {
            OpenRecentMenuItem.Items.Add(CreateWindowRecentRomMenuItem(entry));
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

    private void RequestFullscreenToggle()
    {
        OverflowButton.Flyout?.Hide();
        Dispatcher.UIThread.Post(() => ToggleFullscreen?.Invoke(), DispatcherPriority.Background);
    }

    private void ConfigureWindowMenu()
    {
        FileMenuItem.SubmenuOpened += (_, _) => RefreshRecentRoms?.Invoke();
        OpenRomMenuItem.InputGesture = _openRomGesture;
        CloseWindowMenuItem.InputGesture = _closeGesture;
        ConfigurationMenuItem.InputGesture = _configurationGesture;
        PauseEmulationMenuItem.InputGesture = _pauseGesture;
        ResetEmulationMenuItem.InputGesture = _resetGesture;
        CheatsMenuItem.InputGesture = _cheatsGesture;
        MuteAudioMenuItem.InputGesture = _muteGesture;
        FastForwardMenuItem.InputGesture = _fastForwardGesture;
        FullscreenMenuItem.InputGesture = _fullscreenGesture;
        ConfigureStateSlotMenuItems();
        ConfigureFastForwardSpeedMenuItems();
    }

    private void ConfigureKeyBindings(Window window)
    {
        AddKeyBinding(window, _openRomGesture, OpenRomCommand);
        AddKeyBinding(window, _closeGesture, CloseCommand);
        AddKeyBinding(window, _configurationGesture, ConfigurationCommand);
        AddKeyBinding(window, _pauseGesture, PauseCommand);
        AddKeyBinding(window, _resetGesture, ResetCommand);
        AddKeyBinding(window, _cheatsGesture, CheatsCommand);
        AddKeyBinding(window, _muteGesture, MuteCommand);
        AddKeyBinding(window, _fastForwardGesture, FastForwardCommand);
        AddKeyBinding(window, _fullscreenGesture, FullscreenCommand);
    }

    private static void AddKeyBinding(Window window, KeyGesture gesture, ICommand command) =>
        window.KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = command });

    private void ConfigureStateSlotMenuItems()
    {
        for (var slotIndex = 0; slotIndex < StateSlotCount; slotIndex++)
        {
            var saveItem = CreateWindowStateSlotMenuItem(slotIndex, SaveStateCommand);
            var loadItem = CreateWindowStateSlotMenuItem(slotIndex, LoadStateCommand);

            _saveStateSlotMenuItems[slotIndex] = saveItem;
            _loadStateSlotMenuItems[slotIndex] = loadItem;
            SaveStateMenuItem.Items.Add(saveItem);
            LoadStateMenuItem.Items.Add(loadItem);
        }
    }

    private static MenuItem CreateWindowStateSlotMenuItem(int slotIndex, ICommand command) =>
        new()
        {
            Header = $"Slot {slotIndex + 1}",
            Command = command,
            CommandParameter = slotIndex,
        };

    private void ConfigureFastForwardSpeedMenuItems()
    {
        foreach (var speed in Enum.GetValues<EmulationSpeed>())
        {
            var windowItem = CreateWindowFastForwardSpeedMenuItem(speed);
            _fastForwardSpeedMenuItems.Add((windowItem, speed));
            FastForwardSpeedMenuItem.Items.Add(windowItem);
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

    private sealed class MenuCommand(Action<object?> execute, Func<bool>? canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
