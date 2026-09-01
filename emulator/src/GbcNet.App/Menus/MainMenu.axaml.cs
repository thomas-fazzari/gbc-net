// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.App.Emulation;

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
    internal const int StateSlotCount = 10;

    private readonly List<MenuCommand> _commands = [];
    private readonly ComboBoxItem[] _stateSlotItems = new ComboBoxItem[StateSlotCount];
    private readonly bool[] _stateSlotHasData = new bool[StateSlotCount];
    private readonly EmulationSpeed[] _fastForwardSpeeds = Enum.GetValues<EmulationSpeed>();
    private bool _emulationActionsEnabled;
    private bool _pauseEnabled;
    private bool _cheatsEnabled;
    private bool _synchronizingFastForwardSpeed;
    private int _selectedStateSlotIndex;

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
            _ => SaveState?.Invoke(_selectedStateSlotIndex),
            () => _emulationActionsEnabled
        );
        LoadStateCommand = CreateCommand(
            _ => LoadState?.Invoke(_selectedStateSlotIndex),
            () => _emulationActionsEnabled && _stateSlotHasData[_selectedStateSlotIndex]
        );
        FastForwardCommand = CreateCommand(_ => ToggleFastForward?.Invoke());
        FastForwardSpeedCommand = CreateCommand(parameter =>
        {
            if (parameter is EmulationSpeed speed)
            {
                SetFastForwardSpeed?.Invoke(speed);
            }
        });
        FullscreenCommand = CreateCommand(_ => ToggleFullscreen?.Invoke());
        GitHubRepositoryCommand = CreateCommand(_ => OpenGitHubRepository?.Invoke());

        InitializeComponent();
        ConfigurePanel();
    }

    public event EventHandler? ActionInvoked;

    public Action? OpenRom { get; set; }

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

    public ICommand FullscreenCommand { get; }

    public ICommand GitHubRepositoryCommand { get; }

    public void AttachToWindow(Window window) => ConfigureKeyBindings(window);

    public void SetEmulationActionsEnabled(bool isEnabled)
    {
        _emulationActionsEnabled = isEnabled;
        LibrarySection.IsVisible = !isEnabled;
        EmulationSection.IsVisible = isEnabled;
        SetPauseState(isEnabled: isEnabled, isPaused: false);
        RefreshCommandStates();
    }

    public void SetCheatsEnabled(bool enabled)
    {
        _cheatsEnabled = enabled;
        RefreshCommandStates();
    }

    public void SetStateSlotDates(IReadOnlyList<DateTime?> dates)
    {
        for (var slotIndex = 0; slotIndex < StateSlotCount; slotIndex++)
        {
            var date = dates[index: slotIndex];
            _stateSlotHasData[slotIndex] = date is not null;
            _stateSlotItems[slotIndex].Content = date is not null
                ? $"Slot {slotIndex + 1} — {date:g}"
                : $"Slot {slotIndex + 1}";
        }

        RefreshCommandStates();
    }

    public void SetPauseState(bool isEnabled, bool isPaused)
    {
        _pauseEnabled = isEnabled;
        PauseButton.Content = isPaused ? "Resume" : "Pause";
        RefreshCommandStates();
    }

    public void SetFastForwardState(bool isEnabled, EmulationSpeed speed)
    {
        FastForwardToggleButton.IsChecked = isEnabled;

        var selectedIndex = Array.IndexOf(_fastForwardSpeeds, speed);
        if (selectedIndex < 0 || FastForwardSpeedComboBox.SelectedIndex == selectedIndex)
        {
            return;
        }

        _synchronizingFastForwardSpeed = true;
        FastForwardSpeedComboBox.SelectedIndex = selectedIndex;
        _synchronizingFastForwardSpeed = false;
    }

    public void SetMuteState(bool isMuted) => MuteToggleButton.IsChecked = isMuted;

    private MenuCommand CreateCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        var command = new MenuCommand(
            parameter =>
            {
                execute(parameter);
                ActionInvoked?.Invoke(this, EventArgs.Empty);
            },
            canExecute
        );
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

    private void ConfigurePanel()
    {
        for (var slotIndex = 0; slotIndex < StateSlotCount; slotIndex++)
        {
            var item = new ComboBoxItem { Content = $"Slot {slotIndex + 1}" };
            _stateSlotItems[slotIndex] = item;
            StateSlotComboBox.Items.Add(item);
        }

        StateSlotComboBox.SelectedIndex = 0;

        _synchronizingFastForwardSpeed = true;
        foreach (var speed in _fastForwardSpeeds)
        {
            FastForwardSpeedComboBox.Items.Add(
                new ComboBoxItem { Content = speed.GetDisplayName() }
            );
        }

        FastForwardSpeedComboBox.SelectedIndex = 0;
        _synchronizingFastForwardSpeed = false;
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

    private void OnStateSlotSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedIndex = StateSlotComboBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= StateSlotCount)
        {
            return;
        }

        _selectedStateSlotIndex = selectedIndex;
        RefreshCommandStates();
    }

    private void OnFastForwardSpeedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedIndex = FastForwardSpeedComboBox.SelectedIndex;
        if (
            _synchronizingFastForwardSpeed
            || selectedIndex < 0
            || selectedIndex >= _fastForwardSpeeds.Length
        )
        {
            return;
        }

        FastForwardSpeedCommand.Execute(_fastForwardSpeeds[selectedIndex]);
    }

    private sealed class MenuCommand(Action<object?> execute, Func<bool>? canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
