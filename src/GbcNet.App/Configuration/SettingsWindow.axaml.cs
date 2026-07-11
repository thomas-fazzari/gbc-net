// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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

    private static readonly IReadOnlyList<JoypadButton> _keyboardButtons =
    [
        JoypadButton.Up,
        JoypadButton.Down,
        JoypadButton.Left,
        JoypadButton.Right,
        JoypadButton.A,
        JoypadButton.B,
        JoypadButton.Start,
        JoypadButton.Select,
    ];

    private static readonly IReadOnlyList<JoypadButton> _gamepadButtons =
    [
        JoypadButton.A,
        JoypadButton.B,
        JoypadButton.Start,
        JoypadButton.Select,
    ];

    private readonly InputConfigDraft _inputDraft;
    private readonly GamepadManager _gamepadManager;
    private readonly Grid _bootRomPage;
    private readonly Grid _inputsPage;
    private readonly Grid _keyboardInputPage;
    private readonly Grid _gamepadInputPage;
    private readonly Button _bootRomNavButton;
    private readonly Button _inputsNavButton;
    private readonly Button _keyboardInputTabButton;
    private readonly Button _gamepadInputTabButton;
    private readonly ListBox _profileListBox;
    private readonly Grid _nameEditorPanel;
    private readonly TextBox _profileNameTextBox;
    private readonly TextBlock _profileErrorTextBlock;
    private readonly TextBlock _inputValidationSummaryTextBlock;
    private readonly TextBlock _profileRailHelpTextBlock;
    private readonly StackPanel _keyboardBindingsPanel;
    private readonly StackPanel _gamepadBindingsPanel;
    private readonly ComboBox _gamepadDeviceSelector;
    private readonly TextBlock _gamepadAvailabilityTextBlock;
    private readonly TextBlock _gamepadNoDevicesTextBlock;
    private readonly TextBlock _gamepadUnsupportedTextBlock;
    private readonly Button _newProfileButton;
    private readonly Button _renameProfileButton;
    private readonly Button _deleteProfileButton;
    private readonly Button _setActiveProfileButton;
    private readonly Button _saveButton;
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
        _bootRomPage = this.FindControl<Grid>("BootRomPage")!;
        _inputsPage = this.FindControl<Grid>("InputsPage")!;
        _keyboardInputPage = this.FindControl<Grid>("KeyboardInputPage")!;
        _gamepadInputPage = this.FindControl<Grid>("GamepadInputPage")!;
        _bootRomNavButton = this.FindControl<Button>("BootRomNavButton")!;
        _inputsNavButton = this.FindControl<Button>("InputsNavButton")!;
        _keyboardInputTabButton = this.FindControl<Button>("KeyboardInputTabButton")!;
        _gamepadInputTabButton = this.FindControl<Button>("GamepadInputTabButton")!;
        _profileListBox = this.FindControl<ListBox>("ProfileListBox")!;
        _nameEditorPanel = this.FindControl<Grid>("NameEditorPanel")!;
        _profileNameTextBox = this.FindControl<TextBox>("ProfileNameTextBox")!;
        _profileErrorTextBlock = this.FindControl<TextBlock>("ProfileErrorTextBlock")!;
        _inputValidationSummaryTextBlock = this.FindControl<TextBlock>(
            "InputValidationSummaryTextBlock"
        )!;
        _profileRailHelpTextBlock = this.FindControl<TextBlock>("ProfileRailHelpTextBlock")!;
        _keyboardBindingsPanel = this.FindControl<StackPanel>("KeyboardBindingsPanel")!;
        _gamepadBindingsPanel = this.FindControl<StackPanel>("GamepadBindingsPanel")!;
        _gamepadDeviceSelector = this.FindControl<ComboBox>("GamepadDeviceSelector")!;
        _gamepadAvailabilityTextBlock = this.FindControl<TextBlock>(
            "GamepadAvailabilityTextBlock"
        )!;
        _gamepadNoDevicesTextBlock = this.FindControl<TextBlock>("GamepadNoDevicesTextBlock")!;
        _gamepadUnsupportedTextBlock = this.FindControl<TextBlock>("GamepadUnsupportedTextBlock")!;
        _newProfileButton = this.FindControl<Button>("NewProfileButton")!;
        _renameProfileButton = this.FindControl<Button>("RenameProfileButton")!;
        _deleteProfileButton = this.FindControl<Button>("DeleteProfileButton")!;
        _setActiveProfileButton = this.FindControl<Button>("SetActiveProfileButton")!;
        _saveButton = this.FindControl<Button>("SaveButton")!;

        _inputDraft = new InputConfigDraft(settings.Input);
        DmgBootRomPathTextBox.Text = settings.BootRoms.DmgPath;
        CgbBootRomPathTextBox.Text = settings.BootRoms.CgbPath;
        SgbBootRomPathTextBox.Text = settings.BootRoms.SgbPath;

        BuildBindingRows(InputTab.Keyboard, _keyboardButtons, _keyboardBindingsPanel);
        BuildBindingRows(InputTab.Gamepad, _gamepadButtons, _gamepadBindingsPanel);
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

    private static string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path;

    private void ShowBootRomPage(object? sender, RoutedEventArgs e)
    {
        CancelTransientEdits();
        _bootRomPage.IsVisible = true;
        _inputsPage.IsVisible = false;
        _bootRomNavButton.Classes.Set("selected", value: true);
        _inputsNavButton.Classes.Set("selected", false);
    }

    private void ShowInputsPage(object? sender, RoutedEventArgs e)
    {
        CancelTransientEdits();
        _bootRomPage.IsVisible = false;
        _inputsPage.IsVisible = true;
        _bootRomNavButton.Classes.Set("selected", false);
        _inputsNavButton.Classes.Set("selected", true);
        SelectInputTab(InputTab.Keyboard);
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
        _keyboardInputPage.IsVisible = isKeyboard;
        _gamepadInputPage.IsVisible = !isKeyboard;
        _keyboardInputTabButton.Classes.Set("selected", isKeyboard);
        _gamepadInputTabButton.Classes.Set("selected", !isKeyboard);
        _profileRailHelpTextBlock.Text = isKeyboard
            ? "Select a keyboard profile to edit."
            : "Select a gamepad profile to edit.";
        _profileListBox[AutomationProperties.NameProperty] = isKeyboard
            ? "Keyboard input profiles"
            : "Gamepad input profiles";
        _profileListBox[AutomationProperties.HelpTextProperty] = isKeyboard
            ? "Select a keyboard profile to edit. Selection does not activate it."
            : "Select a gamepad profile to edit. Selection does not activate it.";
        RefreshInputUi();
    }

    private async void BrowseDmgBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
                DmgBootRomPathTextBox,
                $"Select {BootRomConfig.DisplayName(HardwareModel.Dmg)} boot ROM"
            )
            .ConfigureAwait(true);

    private async void BrowseCgbBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
                CgbBootRomPathTextBox,
                $"Select {BootRomConfig.DisplayName(HardwareModel.Cgb)} boot ROM"
            )
            .ConfigureAwait(true);

    private async void BrowseSgbBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
                SgbBootRomPathTextBox,
                $"Select {BootRomConfig.DisplayName(HardwareModel.Sgb)} boot ROM"
            )
            .ConfigureAwait(true);

    private void ClearDmgBootRomPath(object? sender, RoutedEventArgs e) =>
        DmgBootRomPathTextBox.Text = string.Empty;

    private void ClearCgbBootRomPath(object? sender, RoutedEventArgs e) =>
        CgbBootRomPathTextBox.Text = string.Empty;

    private void ClearSgbBootRomPath(object? sender, RoutedEventArgs e) =>
        SgbBootRomPathTextBox.Text = string.Empty;

    private void CancelSettings(object? sender, RoutedEventArgs e) => Close(null);

    private void SaveSettings(object? sender, RoutedEventArgs e)
    {
        if (HasTransientEdit)
        {
            return;
        }

        var errors = _inputDraft.Validate();
        if (errors.Count != 0)
        {
            _inputValidationSummaryTextBlock.Text = string.Join(Environment.NewLine, errors);
            _inputValidationSummaryTextBlock.IsVisible = true;
            return;
        }

        Close(new SettingsConfig(GetBootRomConfig(), _inputDraft.Build()));
    }

    private async Task BrowseBootRomAsync(TextBox pathBox, string title)
    {
        var files = await StorageProvider
            .OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = [_binaryFileType],
                }
            )
            .ConfigureAwait(true);

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
            || _profileListBox.SelectedItem is not ListBoxItem { Tag: string name }
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
        StartNameEdit(NameEditMode.Create, string.Empty);

    private void StartRenameProfile(object? sender, RoutedEventArgs e) =>
        StartNameEdit(NameEditMode.Rename, SelectedProfileName);

    private void StartNameEdit(NameEditMode mode, string name)
    {
        CancelCapture();
        _nameEditMode = mode;
        _profileNameTextBox.Text = name;
        _nameEditorPanel.IsVisible = true;
        _profileErrorTextBlock.IsVisible = false;
        RefreshActionStates();
        _profileNameTextBox.Focus();
        _profileNameTextBox.SelectAll();
    }

    private void CommitProfileNameEdit(object? sender, RoutedEventArgs e) => CommitNameEdit();

    private void HandleProfileNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitNameEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
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
                ? _inputDraft.CreateKeyboardProfile(_profileNameTextBox.Text)
                : _inputDraft.CreateGamepadProfile(_profileNameTextBox.Text),
            NameEditMode.Rename => _activeInputTab == InputTab.Keyboard
                ? _inputDraft.RenameKeyboardProfile(SelectedProfileName, _profileNameTextBox.Text)
                : _inputDraft.RenameGamepadProfile(SelectedProfileName, _profileNameTextBox.Text),
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
        _nameEditorPanel.IsVisible = false;
        if (clearError)
        {
            _profileErrorTextBlock.IsVisible = false;
        }

        RefreshActionStates();
    }

    private async void DeleteSelectedProfileAsync(object? sender, RoutedEventArgs e)
    {
        CancelTransientEdits();
        var name = SelectedProfileName;
        if (!await new DeleteInputProfileWindow(name).ShowDialog<bool>(this).ConfigureAwait(true))
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
                Margin = new Thickness(0, 4, 0, 0),
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
        captureButton.Classes.Set("capturing", value: true);
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
            _keyboardCaptureButtons[target.Button].Classes.Set("error", true);
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
            _gamepadCaptureButtons[target.Button].Classes.Set("error", true);
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
        CaptureButtons(target.Tab)[target.Button].Classes.Set("capturing", false);
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
        _profileListBox.SelectionChanged -= SelectInputProfile;
        try
        {
            _profileListBox.Items.Clear();
            foreach (var profile in CurrentProfiles)
            {
                var item = new ListBoxItem
                {
                    Tag = profile.Name,
                    IsSelected = profile.IsSelected,
                    Padding = new Thickness(8, 6),
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
                _profileListBox.Items.Add(item);
            }
        }
        finally
        {
            _profileListBox.SelectionChanged += SelectInputProfile;
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
        foreach (var button in tab == InputTab.Keyboard ? _keyboardButtons : _gamepadButtons)
        {
            var target = new CaptureTarget(tab, button);
            var captureButton = CaptureButtons(tab)[button];
            captureButton.Content =
                tab == InputTab.Keyboard
                    ? _inputDraft.GetKeyboardBinding(profileName, button).ToString()
                    : DisplayGamepadButton(_inputDraft.GetGamepadBinding(profileName, button));
            captureButton.Classes.Set("error", conflicts.Contains(button));
            captureButton.Classes.Set("capturing", _captureTarget == target);
            captureButton.IsEnabled =
                !IsNameEditing && (tab == InputTab.Keyboard || CanCaptureGamepad);
            captureButton[AutomationProperties.NameProperty] =
                tab == InputTab.Keyboard
                    ? $"Capture {DisplayJoypadButton(button)} key"
                    : $"Capture {DisplayJoypadButton(button)} gamepad button";
            captureButton[AutomationProperties.AutomationIdProperty] = $"{tab}Binding{button}";
            captureButton[AutomationProperties.HelpTextProperty] =
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
        _gamepadDeviceSelector.SelectionChanged -= SelectGamepadDevice;
        try
        {
            _gamepadDeviceSelector.Items.Clear();
            foreach (var device in _gamepadManager.ConnectedDevices)
            {
                _gamepadDeviceSelector.Items.Add(
                    new ComboBoxItem { Tag = device.DeviceId, Content = device.DisplayLabel }
                );
            }

            _gamepadDeviceSelector.SelectedItem = _gamepadDeviceSelector
                .Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    item.Tag is uint id && id == _gamepadManager.SelectedDeviceId
                );
            _gamepadDeviceSelector.IsEnabled =
                _gamepadManager.IsAvailable && _gamepadManager.ConnectedDevices.Length != 0;

            if (_gamepadManager.IsAvailable)
            {
                _gamepadAvailabilityTextBlock.Text = string.Empty;
            }
            else if (_gamepadManager.AvailabilityError is { Length: > 0 } error)
            {
                _gamepadAvailabilityTextBlock.Text = $"Gamepad input is unavailable: {error}";
            }
            else
            {
                _gamepadAvailabilityTextBlock.Text = "Gamepad input is unavailable.";
            }
            _gamepadAvailabilityTextBlock.IsVisible = !_gamepadManager.IsAvailable;
            _gamepadNoDevicesTextBlock.Text =
                "No supported gamepad is connected. Profiles remain editable.";
            _gamepadNoDevicesTextBlock.IsVisible =
                _gamepadManager.IsAvailable && _gamepadManager.ConnectedDevices.Length == 0;
            _gamepadUnsupportedTextBlock.Text =
                _gamepadManager.UnsupportedJoystickNames.Length == 0
                    ? string.Empty
                    : $"Unsupported connected devices: {string.Join(", ", _gamepadManager.UnsupportedJoystickNames)}.";
            _gamepadUnsupportedTextBlock.IsVisible =
                _gamepadManager.UnsupportedJoystickNames.Length != 0;
        }
        finally
        {
            _gamepadDeviceSelector.SelectionChanged += SelectGamepadDevice;
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
            _gamepadDeviceSelector.SelectedItem is ComboBoxItem { Tag: uint id } ? id : null
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
            StringComparison.OrdinalIgnoreCase
        );
        var isActive = string.Equals(
            selected,
            ActiveProfileName,
            StringComparison.OrdinalIgnoreCase
        );
        var locked = HasTransientEdit;

        _newProfileButton.IsEnabled = !locked;
        _renameProfileButton.IsEnabled = !locked && !isDefault;
        _deleteProfileButton.IsEnabled = !locked && !isDefault && !isActive;
        _setActiveProfileButton.IsEnabled = !locked && !isActive;
        _profileListBox.IsEnabled = !IsNameEditing;
        _saveButton.IsEnabled = !locked;
    }

    private void ShowProfileResult(InputEditResult result)
    {
        _profileErrorTextBlock.Text = result.ErrorMessage;
        _profileErrorTextBlock.IsVisible = !result.Succeeded;
    }

    private void SetCaptureError(CaptureTarget target, string? error)
    {
        var errorText = CaptureErrors(target.Tab)[target.Button];
        errorText.Text = error;
        errorText.IsVisible = !string.IsNullOrWhiteSpace(error);
    }

    private void ClearValidationSummary()
    {
        _inputValidationSummaryTextBlock.Text = string.Empty;
        _inputValidationSummaryTextBlock.IsVisible = false;
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
        _gamepadManager.IsAvailable && _gamepadManager.SelectedDeviceId is not null;

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

    private sealed class DeleteInputProfileWindow : Window
    {
        public DeleteInputProfileWindow(string profileName)
        {
            Title = "Delete input profile";
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppChrome.Brush(AppChrome.Bg);
            Content = BuildContent(profileName);
        }

        private StackPanel BuildContent(string profileName)
        {
            var cancelButton = AppChrome.Button("Cancel");
            cancelButton.Click += (_, _) => Close(dialogResult: false);

            var deleteButton = AppChrome.Button("Delete", accent: true);
            deleteButton.Click += (_, _) => Close(dialogResult: true);

            return new StackPanel
            {
                Width = 360,
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Delete this input profile?",
                        Foreground = AppChrome.Brush(AppChrome.Text),
                        FontSize = 16,
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = $"Profile '{profileName}' will be removed from this settings draft.",
                        Foreground = AppChrome.Brush(AppChrome.Muted),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, deleteButton },
                    },
                },
            };
        }
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
