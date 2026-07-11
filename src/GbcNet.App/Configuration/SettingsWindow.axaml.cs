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
    private readonly Grid _bootRomPage;
    private readonly Grid _inputsPage;
    private readonly Button _bootRomNavButton;
    private readonly Button _inputsNavButton;
    private readonly ListBox _profileListBox;
    private readonly Grid _nameEditorPanel;
    private readonly TextBox _profileNameTextBox;
    private readonly TextBlock _profileErrorTextBlock;
    private readonly StackPanel _keyboardBindingsPanel;
    private readonly Button _newProfileButton;
    private readonly Button _renameProfileButton;
    private readonly Button _deleteProfileButton;
    private readonly Button _setActiveProfileButton;
    private readonly Button _saveButton;
    private readonly Dictionary<JoypadButton, Button> _captureButtons = [];
    private readonly Dictionary<JoypadButton, TextBlock> _captureErrors = [];
    private bool _refreshingProfiles;
    private JoypadButton? _capturingButton;
    private NameEditMode _nameEditMode;

    public SettingsWindow(SettingsConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        InitializeComponent();

        _bootRomPage = this.FindControl<Grid>("BootRomPage")!;
        _inputsPage = this.FindControl<Grid>("InputsPage")!;
        _bootRomNavButton = this.FindControl<Button>("BootRomNavButton")!;
        _inputsNavButton = this.FindControl<Button>("InputsNavButton")!;
        _profileListBox = this.FindControl<ListBox>("ProfileListBox")!;
        _nameEditorPanel = this.FindControl<Grid>("NameEditorPanel")!;
        _profileNameTextBox = this.FindControl<TextBox>("ProfileNameTextBox")!;
        _profileErrorTextBlock = this.FindControl<TextBlock>("ProfileErrorTextBlock")!;
        _keyboardBindingsPanel = this.FindControl<StackPanel>("KeyboardBindingsPanel")!;
        _newProfileButton = this.FindControl<Button>("NewProfileButton")!;
        _renameProfileButton = this.FindControl<Button>("RenameProfileButton")!;
        _deleteProfileButton = this.FindControl<Button>("DeleteProfileButton")!;
        _setActiveProfileButton = this.FindControl<Button>("SetActiveProfileButton")!;
        _saveButton = this.FindControl<Button>("SaveButton")!;

        _inputDraft = new InputConfigDraft(settings.Input);
        DmgBootRomPathTextBox.Text = settings.BootRoms.DmgPath;
        CgbBootRomPathTextBox.Text = settings.BootRoms.CgbPath;
        SgbBootRomPathTextBox.Text = settings.BootRoms.SgbPath;

        BuildKeyboardBindingRows();
        RefreshInputUi();
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

        if (files.Count == 0)
        {
            return;
        }

        pathBox.Text = files[0].Path.IsFile ? files[0].Path.LocalPath : files[0].Path.ToString();
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
        ShowProfileResult(_inputDraft.SelectProfile(name));
        RefreshKeyboardBindings();
        RefreshActionStates();
    }

    private void StartNewProfile(object? sender, RoutedEventArgs e) =>
        StartNameEdit(NameEditMode.Create, string.Empty);

    private void StartRenameProfile(object? sender, RoutedEventArgs e) =>
        StartNameEdit(NameEditMode.Rename, _inputDraft.SelectedProfileName);

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
            NameEditMode.Create => _inputDraft.CreateProfile(_profileNameTextBox.Text),
            NameEditMode.Rename => _inputDraft.RenameProfile(
                _inputDraft.SelectedProfileName,
                _profileNameTextBox.Text
            ),
            _ => InputEditResult.Success(),
        };

        if (!result.Succeeded)
        {
            ShowProfileResult(result);
            return;
        }

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
        var name = _inputDraft.SelectedProfileName;
        if (!await new DeleteInputProfileWindow(name).ShowDialog<bool>(this).ConfigureAwait(true))
        {
            return;
        }

        ShowProfileResult(_inputDraft.DeleteProfile(name));
        RefreshInputUi();
    }

    private void SetActiveProfile(object? sender, RoutedEventArgs e)
    {
        CancelTransientEdits();
        ShowProfileResult(_inputDraft.SetActiveProfile(_inputDraft.SelectedProfileName));
        RefreshInputUi();
    }

    private void BuildKeyboardBindingRows()
    {
        foreach (var button in InputConfigValidator.RequiredButtons)
        {
            var captureButton = new Button
            {
                Classes = { "chrome-button", "capture-button" },
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = button,
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

            _keyboardBindingsPanel.Children.Add(
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

            _captureButtons.Add(button, captureButton);
            _captureErrors.Add(button, errorText);
        }
    }

    private void StartCapture(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: JoypadButton button } captureButton)
        {
            return;
        }

        CancelCapture();
        _capturingButton = button;
        captureButton.Classes.Set("capturing", true);
        captureButton.Content = "Press a key…";
        SetCaptureError(button, null);
        RefreshActionStates();
        captureButton.Focus();
    }

    private void HandleCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturingButton is not { } button || sender is not Button { Tag: JoypadButton source })
        {
            return;
        }

        if (button != source)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            CancelCapture();
            return;
        }

        var result = _inputDraft.SetKeyboardBinding(_inputDraft.SelectedProfileName, button, e.Key);
        if (!result.Succeeded)
        {
            SetCaptureError(button, result.ErrorMessage);
            _captureButtons[button].Classes.Set("error", true);
            return;
        }

        CancelCapture();
    }

    private void CancelCaptureOnLostFocus(object? sender, RoutedEventArgs e) => CancelCapture();

    private void CancelCapture()
    {
        if (_capturingButton is not { } button)
        {
            return;
        }

        _capturingButton = null;
        _captureButtons[button].Classes.Set("capturing", false);
        RefreshKeyboardBindings();
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
        RefreshKeyboardBindings();
        RefreshActionStates();
    }

    private void RefreshProfiles()
    {
        _refreshingProfiles = true;
        _profileListBox.Items.Clear();
        foreach (var profile in _inputDraft.Profiles)
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
        _refreshingProfiles = false;
    }

    private void RefreshKeyboardBindings()
    {
        foreach (var button in InputConfigValidator.RequiredButtons)
        {
            var key = _inputDraft.GetKeyboardBinding(_inputDraft.SelectedProfileName, button);
            var captureButton = _captureButtons[button];
            captureButton.Content = key.ToString();
            captureButton.Classes.Set("error", false);
            captureButton.Classes.Set("capturing", _capturingButton == button);
            captureButton.IsEnabled = !IsNameEditing;
            captureButton[AutomationProperties.NameProperty] =
                $"Capture {DisplayJoypadButton(button)} key";
            captureButton[AutomationProperties.AutomationIdProperty] = $"KeyboardBinding{button}";
            captureButton[AutomationProperties.HelpTextProperty] =
                $"Current key is {key}. Press Enter or Space to start capture, Escape to cancel capture.";
            SetCaptureError(button, error: null);
        }
    }

    private void RefreshActionStates()
    {
        var selected = _inputDraft.SelectedProfileName;
        var isDefault = string.Equals(
            selected,
            InputConfig.DefaultProfileName,
            StringComparison.OrdinalIgnoreCase
        );
        var isActive = string.Equals(
            selected,
            _inputDraft.ActiveProfileName,
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

    private void SetCaptureError(JoypadButton button, string? error)
    {
        _captureErrors[button].Text = error;
        _captureErrors[button].IsVisible = !string.IsNullOrWhiteSpace(error);
    }

    private bool HasTransientEdit => IsNameEditing || _capturingButton is not null;

    private bool IsNameEditing => _nameEditMode != NameEditMode.None;

    private static string DisplayJoypadButton(JoypadButton button) =>
        button switch
        {
            JoypadButton.A => "A",
            JoypadButton.B => "B",
            _ => button.ToString(),
        };

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

    private enum NameEditMode
    {
        None = 0,
        Create = 1,
        Rename = 2,
    }
}
