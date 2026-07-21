// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Input;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration;

internal sealed partial class SettingsWindow : Window
{
    private static readonly FilePickerFileType _binaryFileType = new("Binary files")
    {
        Patterns = ["*.bin", "*"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/octet-stream"],
    };
    private readonly InputConfigDraft _inputDraft;
    private readonly GamepadManager _gamepadManager;
    private readonly Dictionary<JoypadButton, Button> _keyboardCaptureButtons = [];
    private readonly Dictionary<JoypadButton, TextBlock> _keyboardCaptureErrors = [];
    private readonly Dictionary<JoypadButton, Button> _gamepadCaptureButtons = [];
    private readonly Dictionary<JoypadButton, TextBlock> _gamepadCaptureErrors = [];
    private bool _refreshingProfiles;
    private bool _refreshingDevices;
    private InputTab _activeInputTab = InputTab.Keyboard;
    private CaptureTarget? _captureTarget;
    private NameEditMode _nameEditMode;

    public SettingsWindow(SettingsConfig settings, GamepadManager gamepadManager)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(gamepadManager);

        InitializeComponent();

        _gamepadManager = gamepadManager;

        _inputDraft = new InputConfigDraft(settings.Input);
        DmgBootRomPathTextBox.Text = settings.BootRoms.DmgPath;
        CgbBootRomPathTextBox.Text = settings.BootRoms.CgbPath;
        SgbBootRomPathTextBox.Text = settings.BootRoms.SgbPath;
        VolumeSlider.Value = settings.Audio.VolumePercent;
        VolumeValueTextBlock.Text = $"{settings.Audio.VolumePercent}%";
        MuteAudioCheckBox.IsChecked = settings.Audio.Muted;

        BuildBindingRows(
            InputTab.Keyboard,
            InputConfigMetadata.KeyboardButtons,
            KeyboardBindingsPanel
        );
        BuildBindingRows(
            InputTab.Gamepad,
            InputConfigMetadata.GamepadButtons,
            GamepadBindingsPanel
        );

        _gamepadManager.DevicesChanged += HandleGamepadDevicesChanged;
        _gamepadManager.AllowedButtonPressed += HandleAllowedGamepadButtonPressed;
        Closed += OnSettingsClosed;
        AddHandler(KeyDownEvent, HandleWindowKeyDown, RoutingStrategies.Tunnel);
        LostFocus += CancelCaptureOnWindowLostFocus;

        SelectInputTab(InputTab.Keyboard);
        RefreshGamepadDevices();
    }

    private BootRomConfig GetBootRomConfig() =>
        new(
            NormalizePath(DmgBootRomPathTextBox.Text),
            NormalizePath(CgbBootRomPathTextBox.Text),
            NormalizePath(SgbBootRomPathTextBox.Text)
        );

    private AudioConfig GetAudioConfig() =>
        new(
            VolumePercent: (int)Math.Round(VolumeSlider.Value),
            Muted: MuteAudioCheckBox.IsChecked is true
        );

    private static string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path;

    private void ShowBootRomPage(object? sender, RoutedEventArgs e) =>
        ShowPage(BootRomPage, BootRomNavButton);

    private void ShowInputsPage(object? sender, RoutedEventArgs e)
    {
        ShowPage(InputsPage, InputsNavButton);
        SelectInputTab(InputTab.Keyboard);
    }

    private void ShowAudioPage(object? sender, RoutedEventArgs e) =>
        ShowPage(AudioPage, AudioNavButton);

    private void ShowPage(Control page, Button navButton)
    {
        CancelTransientEdits();
        BootRomPage.IsVisible = ReferenceEquals(page, BootRomPage);
        InputsPage.IsVisible = ReferenceEquals(page, InputsPage);
        AudioPage.IsVisible = ReferenceEquals(page, AudioPage);
        BootRomNavButton.Classes.Set(
            name: "selected",
            value: ReferenceEquals(navButton, BootRomNavButton)
        );
        InputsNavButton.Classes.Set(
            name: "selected",
            value: ReferenceEquals(navButton, InputsNavButton)
        );
        AudioNavButton.Classes.Set(
            name: "selected",
            value: ReferenceEquals(navButton, AudioNavButton)
        );
    }

    private void ShowKeyboardInputTab(object? sender, RoutedEventArgs e) =>
        SelectInputTab(InputTab.Keyboard);

    private void ShowGamepadInputTab(object? sender, RoutedEventArgs e) =>
        SelectInputTab(InputTab.Gamepad);

    private void SelectInputTab(InputTab tab)
    {
        CancelTransientEdits();
        _activeInputTab = tab;
        var isKeyboard = tab == InputTab.Keyboard;
        KeyboardInputPage.IsVisible = isKeyboard;
        GamepadInputPage.IsVisible = !isKeyboard;
        KeyboardInputTabButton.Classes.Set(name: "selected", value: isKeyboard);
        GamepadInputTabButton.Classes.Set(name: "selected", value: !isKeyboard);
        ProfileRailHelpTextBlock.Text = isKeyboard
            ? "Select a keyboard profile to edit."
            : "Select a gamepad profile to edit.";
        ProfileListBox[property: AutomationProperties.NameProperty] = isKeyboard
            ? "Keyboard input profiles"
            : "Gamepad input profiles";
        ProfileListBox[property: AutomationProperties.HelpTextProperty] = isKeyboard
            ? "Select a keyboard profile to edit. Selection does not activate it."
            : "Select a gamepad profile to edit. Selection does not activate it.";
        RefreshInputUi();
    }

    private async void BrowseDmgBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
            DmgBootRomPathTextBox,
            $"Select {BootRomConfig.DisplayName(HardwareModel.Dmg)} boot ROM"
        );

    private async void BrowseCgbBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
            CgbBootRomPathTextBox,
            $"Select {BootRomConfig.DisplayName(HardwareModel.Cgb)} boot ROM"
        );

    private async void BrowseSgbBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
            SgbBootRomPathTextBox,
            $"Select {BootRomConfig.DisplayName(HardwareModel.Sgb)} boot ROM"
        );

    private void ClearDmgBootRomPath(object? sender, RoutedEventArgs e) =>
        DmgBootRomPathTextBox.Text = string.Empty;

    private void ClearCgbBootRomPath(object? sender, RoutedEventArgs e) =>
        CgbBootRomPathTextBox.Text = string.Empty;

    private void ClearSgbBootRomPath(object? sender, RoutedEventArgs e) =>
        SgbBootRomPathTextBox.Text = string.Empty;

    private void UpdateVolumeValue(object? sender, RangeBaseValueChangedEventArgs e) =>
        VolumeValueTextBlock.Text = $"{Math.Round(e.NewValue)}%";

    private void CancelSettings(object? sender, RoutedEventArgs e) => Close(dialogResult: null);

    private void SaveSettings(object? sender, RoutedEventArgs e)
    {
        if (HasTransientEdit)
        {
            return;
        }

        var errors = _inputDraft.Validate();
        if (errors.Count != 0)
        {
            InputValidationSummaryTextBlock.Text = string.Join(Environment.NewLine, errors);
            InputValidationSummaryTextBlock.IsVisible = true;
            return;
        }

        Close(
            new SettingsConfig(GetBootRomConfig(), _inputDraft.Build()) { Audio = GetAudioConfig() }
        );
    }

    private async Task BrowseBootRomAsync(TextBox pathBox, string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [_binaryFileType],
            }
        );

        if (files.Count != 0)
        {
            pathBox.Text = files[0].Path.IsFile
                ? files[0].Path.LocalPath
                : files[0].Path.ToString();
        }
    }

    private void SelectInputProfile(object? sender, SelectionChangedEventArgs e)
    {
        if (
            _refreshingProfiles
            || ProfileListBox.SelectedItem is not ListBoxItem { Tag: string name }
        )
        {
            return;
        }

        CancelTransientEdits();
        ShowProfileResult(
            _activeInputTab == InputTab.Keyboard
                ? _inputDraft.SelectKeyboardProfile(name)
                : _inputDraft.SelectGamepadProfile(name)
        );
        RefreshBindings(_activeInputTab);
        RefreshActionStates();
    }

    private void StartNewProfile(object? sender, RoutedEventArgs e) =>
        StartNameEdit(NameEditMode.Create, name: string.Empty);

    private void StartRenameProfile(object? sender, RoutedEventArgs e) =>
        StartNameEdit(NameEditMode.Rename, SelectedProfileName);

    private void StartNameEdit(NameEditMode mode, string name)
    {
        CancelCapture();
        _nameEditMode = mode;
        ProfileNameTextBox.Text = name;
        NameEditorPanel.IsVisible = true;
        ProfileErrorTextBlock.IsVisible = false;
        RefreshActionStates();
        ProfileNameTextBox.Focus();
        ProfileNameTextBox.SelectAll();
    }

    private void CommitProfileNameEdit(object? sender, RoutedEventArgs e) => CommitNameEdit();

    private void HandleProfileNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            CommitNameEdit();
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            CancelNameEdit();
            e.Handled = true;
        }
    }

    private void CommitNameEdit()
    {
        var result = _nameEditMode switch
        {
            NameEditMode.Create => _activeInputTab == InputTab.Keyboard
                ? _inputDraft.CreateKeyboardProfile(ProfileNameTextBox.Text)
                : _inputDraft.CreateGamepadProfile(ProfileNameTextBox.Text),
            NameEditMode.Rename => _activeInputTab == InputTab.Keyboard
                ? _inputDraft.RenameKeyboardProfile(SelectedProfileName, ProfileNameTextBox.Text)
                : _inputDraft.RenameGamepadProfile(SelectedProfileName, ProfileNameTextBox.Text),
            _ => InputEditResult.Success(),
        };

        if (!result.Succeeded)
        {
            ShowProfileResult(result);
            return;
        }

        ClearValidationSummary();
        CancelNameEdit(clearError: false);
        RefreshInputUi();
    }

    private void CancelProfileNameEdit(object? sender, RoutedEventArgs e) => CancelNameEdit();

    private void CancelNameEdit(bool clearError = true)
    {
        _nameEditMode = NameEditMode.None;
        NameEditorPanel.IsVisible = false;
        if (clearError)
        {
            ProfileErrorTextBlock.IsVisible = false;
        }

        RefreshActionStates();
    }

    private async void DeleteSelectedProfileAsync(object? sender, RoutedEventArgs e)
    {
        CancelTransientEdits();
        var name = SelectedProfileName;
        if (
            !await new DestructiveConfirmationWindow(
                title: "Delete input profile",
                heading: "Delete this input profile?",
                message: $"Profile '{name}' will be removed from this settings draft.",
                destructiveButtonLabel: "Delete"
            ).ShowDialog<bool>(this)
        )
        {
            return;
        }

        ShowProfileResult(
            _activeInputTab == InputTab.Keyboard
                ? _inputDraft.DeleteKeyboardProfile(name)
                : _inputDraft.DeleteGamepadProfile(name)
        );
        ClearValidationSummary();
        RefreshInputUi();
    }

    private void SetActiveProfile(object? sender, RoutedEventArgs e)
    {
        CancelTransientEdits();
        ShowProfileResult(
            _activeInputTab == InputTab.Keyboard
                ? _inputDraft.SetActiveKeyboardProfile(SelectedProfileName)
                : _inputDraft.SetActiveGamepadProfile(SelectedProfileName)
        );
        ClearValidationSummary();
        RefreshInputUi();
    }

    private void BuildBindingRows(
        InputTab tab,
        IEnumerable<JoypadButton> buttons,
        StackPanel bindingPanel
    )
    {
        foreach (var button in buttons)
        {
            var captureButton = new Button
            {
                Classes = { "chrome-button", "capture-button" },
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = new CaptureTarget(tab, button),
            };
            captureButton.AddHandler(KeyDownEvent, HandleCaptureKeyDown, RoutingStrategies.Tunnel);
            captureButton.Click += StartCapture;
            captureButton.LostFocus += CancelCaptureOnLostFocus;

            var errorText = new TextBlock
            {
                Classes = { "error" },
                IsVisible = false,
                Margin = new Thickness(left: 0, top: 4, right: 0, bottom: 0),
            };

            bindingPanel.Children.Add(
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("100,*"),
                    RowDefinitions = new RowDefinitions("Auto,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = DisplayJoypadButton(button),
                            Classes = { "chrome-label", "form-label" },
                        },
                        captureButton,
                        errorText,
                    },
                }
            );
            Grid.SetColumn(captureButton, 1);
            Grid.SetColumn(errorText, 1);
            Grid.SetRow(errorText, 1);

            CaptureButtons(tab).Add(button, captureButton);
            CaptureErrors(tab).Add(button, errorText);
        }
    }

    private void StartCapture(object? sender, RoutedEventArgs e)
    {
        if (
            sender is not Button { Tag: CaptureTarget target } captureButton
            || target.Tab != _activeInputTab
        )
        {
            return;
        }

        if (target.Tab == InputTab.Gamepad && !CanCaptureGamepad)
        {
            return;
        }

        CancelCapture();
        _captureTarget = target;
        captureButton.Classes.Set(name: "capturing", value: true);
        captureButton.Content =
            target.Tab == InputTab.Keyboard ? "Press a key…" : "Press a button…";
        SetCaptureError(target, error: null);
        RefreshActionStates();
        captureButton.Focus();
    }

    private void HandleCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (
            _captureTarget is not { Tab: InputTab.Keyboard } target
            || sender is not Button { Tag: CaptureTarget source }
            || target != source
        )
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            CancelCapture();
            return;
        }

        var result = _inputDraft.SetKeyboardBinding(SelectedProfileName, target.Button, e.Key);
        if (!result.Succeeded)
        {
            SetCaptureError(target, result.ErrorMessage);
            _keyboardCaptureButtons[target.Button].Classes.Set(name: "error", value: true);
            return;
        }

        ClearValidationSummary();
        CancelCapture();
    }

    private void HandleWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_captureTarget is not null && e.Key == Key.Escape)
        {
            CancelCapture();
            e.Handled = true;
        }
    }

    private void HandleAllowedGamepadButtonPressed(object? sender, GamepadButtonPressedEventArgs e)
    {
        if (_captureTarget is not { Tab: InputTab.Gamepad } target || !CanCaptureGamepad)
        {
            return;
        }

        var result = _inputDraft.SetGamepadBinding(SelectedProfileName, target.Button, e.Button);
        if (!result.Succeeded)
        {
            SetCaptureError(target, result.ErrorMessage);
            _gamepadCaptureButtons[target.Button].Classes.Set(name: "error", value: true);
            return;
        }

        ClearValidationSummary();
        CancelCapture();
    }

    private void CancelCaptureOnLostFocus(object? sender, RoutedEventArgs e) => CancelCapture();

    private void CancelCaptureOnWindowLostFocus(object? sender, RoutedEventArgs e) =>
        CancelCapture();

    private void CancelCapture()
    {
        if (_captureTarget is not { } target)
        {
            return;
        }

        _captureTarget = null;
        CaptureButtons(target.Tab)[target.Button].Classes.Set(name: "capturing", value: false);
        RefreshBindings(target.Tab);
        RefreshActionStates();
    }

    private void CancelTransientEdits()
    {
        CancelCapture();
        CancelNameEdit();
    }

    private void RefreshInputUi()
    {
        RefreshProfiles();
        RefreshBindings(_activeInputTab);
        RefreshGamepadDevices();
        RefreshActionStates();
    }

    private void RefreshProfiles()
    {
        _refreshingProfiles = true;
        ProfileListBox.SelectionChanged -= SelectInputProfile;
        try
        {
            ProfileListBox.Items.Clear();
            foreach (var profile in CurrentProfiles)
            {
                var item = new ListBoxItem
                {
                    Tag = profile.Name,
                    IsSelected = profile.IsSelected,
                    Padding = new Thickness(horizontal: 8, vertical: 6),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = profile.Name,
                                Foreground = AppChrome.Brush(AppChrome.Text),
                                TextTrimming = TextTrimming.CharacterEllipsis,
                            },
                            new TextBlock
                            {
                                Text = profile.IsActive ? "Active" : string.Empty,
                                Foreground = AppChrome.Brush(AppChrome.Muted),
                                FontSize = 11,
                                FontWeight = FontWeight.SemiBold,
                            },
                        },
                    },
                };
                Grid.SetColumn(((Grid)item.Content).Children[1], 1);
                ProfileListBox.Items.Add(item);
            }
        }
        finally
        {
            ProfileListBox.SelectionChanged += SelectInputProfile;
            _refreshingProfiles = false;
        }
    }

    private void RefreshBindings(InputTab tab)
    {
        var profileName = GetSelectedProfileName(tab);
        var conflicts =
            tab == InputTab.Keyboard
                ? _inputDraft.KeyboardBindingConflicts
                : _inputDraft.GamepadBindingConflicts;
        foreach (
            var button in tab == InputTab.Keyboard
                ? InputConfigMetadata.KeyboardButtons
                : InputConfigMetadata.GamepadButtons
        )
        {
            var target = new CaptureTarget(tab, button);
            var captureButton = CaptureButtons(tab)[button];
            captureButton.Content =
                tab == InputTab.Keyboard
                    ? _inputDraft.GetKeyboardBinding(profileName, button).ToString()
                    : DisplayGamepadButton(_inputDraft.GetGamepadBinding(profileName, button));
            captureButton.Classes.Set(name: "error", value: conflicts.Contains(button));
            captureButton.Classes.Set(name: "capturing", value: _captureTarget == target);
            captureButton.IsEnabled =
                !IsNameEditing && (tab == InputTab.Keyboard || CanCaptureGamepad);
            captureButton[property: AutomationProperties.NameProperty] =
                tab == InputTab.Keyboard
                    ? $"Capture {DisplayJoypadButton(button)} key"
                    : $"Capture {DisplayJoypadButton(button)} gamepad button";
            captureButton[property: AutomationProperties.AutomationIdProperty] =
                $"{tab}Binding{button}";
            captureButton[property: AutomationProperties.HelpTextProperty] =
                tab == InputTab.Keyboard
                    ? "Press Enter or Space to start key capture; press Escape to cancel."
                    : "Press Enter or Space to start button capture; press Escape to cancel.";
            SetCaptureError(
                target,
                conflicts.Contains(button)
                    ? "This binding is assigned more than once. Resolve it before saving."
                    : null
            );
        }
    }

    private void RefreshGamepadDevices()
    {
        _refreshingDevices = true;
        GamepadDeviceSelector.SelectionChanged -= SelectGamepadDevice;
        try
        {
            GamepadDeviceSelector.Items.Clear();
            foreach (var device in _gamepadManager.ConnectedDevices)
            {
                GamepadDeviceSelector.Items.Add(
                    new ComboBoxItem { Tag = device.DeviceId, Content = device.DisplayLabel }
                );
            }

            GamepadDeviceSelector.SelectedItem = GamepadDeviceSelector
                .Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    item.Tag is uint id && id == _gamepadManager.SelectedDeviceId
                );
            GamepadDeviceSelector.IsEnabled =
                _gamepadManager.IsAvailable && _gamepadManager.ConnectedDevices.Length != 0;

            if (_gamepadManager.IsAvailable)
            {
                GamepadAvailabilityTextBlock.Text = string.Empty;
            }
            else if (_gamepadManager.AvailabilityError is { Length: > 0 } error)
            {
                GamepadAvailabilityTextBlock.Text = $"Gamepad input is unavailable: {error}";
            }
            else
            {
                GamepadAvailabilityTextBlock.Text = "Gamepad input is unavailable.";
            }
            GamepadAvailabilityTextBlock.IsVisible = !_gamepadManager.IsAvailable;
            GamepadNoDevicesTextBlock.Text =
                "No supported gamepad is connected. Profiles remain editable.";
            GamepadNoDevicesTextBlock.IsVisible =
                _gamepadManager is { IsAvailable: true, ConnectedDevices.Length: 0 };
            GamepadUnsupportedTextBlock.Text =
                _gamepadManager.UnsupportedJoystickNames.Length == 0
                    ? string.Empty
                    : $"Unsupported connected devices: {string.Join(", ", _gamepadManager.UnsupportedJoystickNames)}.";
            GamepadUnsupportedTextBlock.IsVisible =
                _gamepadManager.UnsupportedJoystickNames.Length != 0;
        }
        finally
        {
            GamepadDeviceSelector.SelectionChanged += SelectGamepadDevice;
            _refreshingDevices = false;
        }
    }

    private void SelectGamepadDevice(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingDevices)
        {
            return;
        }

        CancelCapture();
        _gamepadManager.SetSelectedDevice(
            GamepadDeviceSelector.SelectedItem is ComboBoxItem { Tag: uint id } ? id : null
        );
        RefreshBindings(InputTab.Gamepad);
    }

    private void HandleGamepadDevicesChanged(object? sender, EventArgs e)
    {
        if (_captureTarget is { Tab: InputTab.Gamepad } && !CanCaptureGamepad)
        {
            CancelCapture();
        }

        RefreshGamepadDevices();
        RefreshBindings(InputTab.Gamepad);
    }

    private void RefreshActionStates()
    {
        var selected = SelectedProfileName;
        var isDefault = string.Equals(
            selected,
            InputConfig.DefaultProfileName,
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
        var isActive = string.Equals(
            selected,
            ActiveProfileName,
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
        var locked = HasTransientEdit;

        NewProfileButton.IsEnabled = !locked;
        RenameProfileButton.IsEnabled = !locked && !isDefault;
        DeleteProfileButton.IsEnabled = !locked && !isDefault && !isActive;
        SetActiveProfileButton.IsEnabled = !locked && !isActive;
        ProfileListBox.IsEnabled = !IsNameEditing;
        SaveButton.IsEnabled = !locked;
    }

    private void ShowProfileResult(InputEditResult result)
    {
        ProfileErrorTextBlock.Text = result.ErrorMessage;
        ProfileErrorTextBlock.IsVisible = !result.Succeeded;
    }

    private void SetCaptureError(CaptureTarget target, string? error)
    {
        var errorText = CaptureErrors(target.Tab)[target.Button];
        errorText.Text = error;
        errorText.IsVisible = !string.IsNullOrWhiteSpace(error);
    }

    private void ClearValidationSummary()
    {
        InputValidationSummaryTextBlock.Text = string.Empty;
        InputValidationSummaryTextBlock.IsVisible = false;
    }

    private Dictionary<JoypadButton, Button> CaptureButtons(InputTab tab) =>
        tab == InputTab.Keyboard ? _keyboardCaptureButtons : _gamepadCaptureButtons;

    private Dictionary<JoypadButton, TextBlock> CaptureErrors(InputTab tab) =>
        tab == InputTab.Keyboard ? _keyboardCaptureErrors : _gamepadCaptureErrors;

    private IReadOnlyList<InputProfileSummary> CurrentProfiles =>
        _activeInputTab == InputTab.Keyboard
            ? _inputDraft.KeyboardProfiles
            : _inputDraft.GamepadProfiles;

    private string SelectedProfileName =>
        _activeInputTab == InputTab.Keyboard
            ? _inputDraft.SelectedKeyboardProfileName
            : _inputDraft.SelectedGamepadProfileName;

    private string GetSelectedProfileName(InputTab tab) =>
        tab == InputTab.Keyboard
            ? _inputDraft.SelectedKeyboardProfileName
            : _inputDraft.SelectedGamepadProfileName;

    private string ActiveProfileName =>
        _activeInputTab == InputTab.Keyboard
            ? _inputDraft.ActiveKeyboardProfileName
            : _inputDraft.ActiveGamepadProfileName;

    private bool CanCaptureGamepad =>
        _gamepadManager is { IsAvailable: true, SelectedDeviceId: not null };

    private bool HasTransientEdit => IsNameEditing || _captureTarget is not null;

    private bool IsNameEditing => _nameEditMode != NameEditMode.None;

    private string DisplayGamepadButton(GamepadButton button) =>
        _gamepadManager.SelectedDeviceId is { } deviceId
            ? _gamepadManager.GetButtonDisplayLabel(deviceId, button)
            : button.ToString();

    private static string DisplayJoypadButton(JoypadButton button) =>
        button switch
        {
            JoypadButton.A => "A",
            JoypadButton.B => "B",
            _ => button.ToString(),
        };

    private void OnSettingsClosed(object? sender, EventArgs e)
    {
        _gamepadManager.DevicesChanged -= HandleGamepadDevicesChanged;
        _gamepadManager.AllowedButtonPressed -= HandleAllowedGamepadButtonPressed;
        Closed -= OnSettingsClosed;
    }

    private readonly record struct CaptureTarget(InputTab Tab, JoypadButton Button);

    private enum InputTab
    {
        Keyboard = 0,
        Gamepad = 1,
    }

    private enum NameEditMode
    {
        None = 0,
        Create = 1,
        Rename = 2,
    }
}
