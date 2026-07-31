// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using GbcNet.Core.Cheats;

namespace GbcNet.App.Cheats;

internal sealed partial class CheatsWindow : Window
{
    private const string InvalidCodeMessage = "Enter a valid 6- or 9-digit Game Genie code.";
    private const string DuplicateCodeMessage = "That code is already in the list.";
    private const string DraftCodeErrorMessage = "Fix invalid or duplicate Game Genie codes.";

    private readonly List<CheatDraft> _entries;
    private readonly List<EntryRowControls> _entryRows = [];
    private readonly Func<IReadOnlyList<GameGenieCodeEntry>, Task> _applyAsync;
    private bool _isApplying;

    internal CheatsWindow(
        IReadOnlyList<GameGenieCodeEntry> entries,
        Func<IReadOnlyList<GameGenieCodeEntry>, Task> applyAsync
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(applyAsync);

        InitializeComponent();

        _entries = new List<CheatDraft>(entries.Count);
        foreach (var entry in entries)
        {
            _entries.Add(
                new CheatDraft(entry.Code.CanonicalCode, entry.IsEnabled, NormalizeName(entry.Name))
            );
        }
        _applyAsync = applyAsync;

        Closing += GuardCloseWhileApplying;
        Opened += (_, _) => FocusInitialControl();
        AddHandler(KeyDownEvent, HandleWindowKeyDown, RoutingStrategies.Tunnel);

        RefreshEntries();
    }

    private void ShowGameGeniePage(object? sender, RoutedEventArgs e) => FocusInitialControl();

    private void ValidateCodeInput(object? sender, TextChangedEventArgs e)
    {
        var text = CodeTextBox.Text;
        if (string.IsNullOrEmpty(text) || GameGenieCode.TryParse(text, out _))
        {
            HideInputError();
            return;
        }

        ShowInputError(InvalidCodeMessage);
    }

    private void AddCode(object? sender, RoutedEventArgs e)
    {
        if (!GameGenieCode.TryParse(CodeTextBox.Text, out var code))
        {
            ShowInputError(InvalidCodeMessage);
            return;
        }

        if (HasEffectiveDuplicate(code))
        {
            ShowInputError(DuplicateCodeMessage);
            return;
        }

        _entries.Add(
            new CheatDraft(code.CanonicalCode, isEnabled: true, NormalizeName(NameTextBox.Text))
        );
        NameTextBox.Text = string.Empty;
        CodeTextBox.Text = string.Empty;
        HideInputError();
        RefreshEntries();
        PostFocus(NameTextBox.IsEnabled ? NameTextBox : ApplyButton);
    }

    private void MoveEntry(int index, int offset)
    {
        var newIndex = index + offset;
        var entry = _entries[index];
        _entries.RemoveAt(index);
        _entries.Insert(newIndex, entry);
        RefreshEntries();
        var action = offset < 0 ? RowAction.MoveUp : RowAction.MoveDown;
        if (newIndex == 0)
        {
            action = RowAction.MoveDown;
        }
        else if (newIndex == _entries.Count - 1)
        {
            action = RowAction.MoveUp;
        }

        FocusRowAction(newIndex, action);
    }

    private void RemoveEntry(int index)
    {
        _entries.RemoveAt(index);
        RefreshEntries();

        if (_entries.Count == 0)
        {
            PostFocus(NameTextBox);
            return;
        }

        FocusRowAction(Math.Min(index, _entries.Count - 1), RowAction.Remove);
    }

    private async void ApplyAsync(object? sender, RoutedEventArgs e)
    {
        if (!ValidateEntryCodes())
        {
            FocusFirstInvalidCode();
            return;
        }

        var entries = CreateEntries();
        _isApplying = true;
        SetDraftEnabled(enabled: false);
        ApplyErrorTextBlock.IsVisible = false;
        ApplyErrorTextBlock.Text = string.Empty;
        ApplyingTextBlock.IsVisible = true;
        ApplyingTextBlock.Text = "Applying...";

        try
        {
            await _applyAsync(entries);
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or OperationCanceledException
            )
        {
            _isApplying = false;
            ApplyingTextBlock.IsVisible = false;
            ApplyingTextBlock.Text = string.Empty;
            ApplyErrorTextBlock.IsVisible = true;
            ApplyErrorTextBlock.Text =
                $"Game Genie codes could not be applied: {exception.Message}";
            SetDraftEnabled(enabled: true);
            ApplyButton.Focus();
            return;
        }

        _isApplying = false;
        Closing -= GuardCloseWhileApplying;
        Close(dialogResult: true);
    }

    private void Cancel(object? sender, RoutedEventArgs e) => Close(dialogResult: null);

    private void HandleWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Escape)
        {
            return;
        }

        e.Handled = true;
        if (!_isApplying)
        {
            Close(dialogResult: null);
        }
    }

    private void GuardCloseWhileApplying(object? sender, WindowClosingEventArgs e)
    {
        if (_isApplying)
        {
            e.Cancel = true;
        }
    }

    private void RefreshEntries()
    {
        EntriesPanel.Children.Clear();
        _entryRows.Clear();

        for (var index = 0; index < _entries.Count; index++)
        {
            AddEntryRow(index);
        }

        var hasEntries = _entries.Count != 0;
        EmptyStateTextBlock.IsVisible = !hasEntries;
        EntriesScrollViewer.IsVisible = hasEntries;
        CounterTextBlock.Text = $"{_entries.Count} / {GameGenieService.MaxEntryCount}";
        RefreshLimitState();
        ValidateEntryCodes();
    }

    private void AddEntryRow(int index)
    {
        var entry = _entries[index];
        var codeTextBox = new TextBox
        {
            Classes = { "code-input" },
            MaxLength = 32,
            Text = entry.CodeText,
            PlaceholderText = "ABC-DEF or ABC-DEF-GHI",
        };
        var codeEditor = new Border
        {
            Classes = { "input-well" },
            Width = 144,
            Child = codeTextBox,
        };

        var nameTextBox = new TextBox
        {
            Classes = { "code-input" },
            MaxLength = GameGenieService.MaxNameLength,
            Text = entry.Name,
            PlaceholderText = "Name (optional)",
        };
        var nameEditor = new Border { Classes = { "input-well" }, Child = nameTextBox };

        var toggle = new ToggleSwitch
        {
            Classes = { "code-toggle" },
            IsChecked = entry.IsEnabled,
            OnContent = "On",
            OffContent = "Off",
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var moveUpButton = CreateRowActionButton("Move up");
        moveUpButton.IsEnabled = index != 0;
        moveUpButton.Click += (_, _) => MoveEntry(index, offset: -1);

        var moveDownButton = CreateRowActionButton("Move down");
        moveDownButton.IsEnabled = index != _entries.Count - 1;
        moveDownButton.Click += (_, _) => MoveEntry(index, offset: 1);

        var removeButton = CreateRowActionButton("Remove");
        removeButton.Click += (_, _) => RemoveEntry(index);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { moveUpButton, moveDownButton, removeButton },
        };

        void UpdateAutomationNames()
        {
            var context = GameGenieCode.TryParse(entry.CodeText, out var code)
                ? code.CanonicalCode
                : $"entry {index + 1}";
            codeTextBox[property: AutomationProperties.NameProperty] = $"Game Genie code {context}";
            nameTextBox[property: AutomationProperties.NameProperty] = $"Name for {context}";
            toggle[property: AutomationProperties.NameProperty] = $"Enable {context}";
            moveUpButton[property: AutomationProperties.NameProperty] = $"Move {context} up";
            moveDownButton[property: AutomationProperties.NameProperty] = $"Move {context} down";
            removeButton[property: AutomationProperties.NameProperty] = $"Remove {context}";
        }

        codeTextBox[property: AutomationProperties.HelpTextProperty] =
            "Enter a six- or nine-digit Game Genie code";
        codeTextBox.TextChanged += (_, _) =>
        {
            entry.CodeText = codeTextBox.Text ?? string.Empty;
            UpdateAutomationNames();
            ValidateEntryCodes();
        };
        nameTextBox[property: AutomationProperties.HelpTextProperty] =
            "Optional name for this Game Genie code";
        nameTextBox.TextChanged += (_, _) => entry.Name = NormalizeName(nameTextBox.Text);
        toggle.IsCheckedChanged += (_, _) => entry.IsEnabled = toggle.IsChecked is true;
        UpdateAutomationNames();

        var rowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 8,
            Children = { codeEditor, nameEditor, toggle, actions },
        };
        Grid.SetColumn(nameEditor, 1);
        Grid.SetColumn(toggle, 2);
        Grid.SetColumn(actions, 3);

        EntriesPanel.Children.Add(
            new Border
            {
                Classes = { "code-row" },
                BorderThickness =
                    index == _entries.Count - 1
                        ? new Thickness(0)
                        : new Thickness(left: 0, top: 0, right: 0, bottom: 1),
                Child = rowGrid,
            }
        );
        _entryRows.Add(
            new EntryRowControls(
                codeTextBox,
                codeEditor,
                nameTextBox,
                toggle,
                moveUpButton,
                moveDownButton,
                removeButton
            )
        );
    }

    private static Button CreateRowActionButton(string content) =>
        new()
        {
            Classes = { "chrome-button", "row-action" },
            Content = content,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static bool IsEffectiveDuplicate(GameGenieCode left, GameGenieCode right) =>
        left.Address == right.Address
        && left.ReplacementValue == right.ReplacementValue
        && left.CompareValue == right.CompareValue;

    private bool HasEffectiveDuplicate(GameGenieCode code)
    {
        foreach (var entry in _entries)
        {
            if (
                GameGenieCode.TryParse(entry.CodeText, out var existing)
                && IsEffectiveDuplicate(existing, code)
            )
            {
                return true;
            }
        }

        return false;
    }

    private bool ValidateEntryCodes()
    {
        foreach (var row in _entryRows)
        {
            row.CodeEditorBorder.Classes.Remove("error");
            row.CodeEditor[property: AutomationProperties.HelpTextProperty] =
                "Enter a six- or nine-digit Game Genie code";
        }

        var firstIndexByCode =
            new Dictionary<(ushort Address, byte ReplacementValue, byte? CompareValue), int>();
        var allValid = true;

        for (var index = 0; index < _entries.Count; index++)
        {
            if (!GameGenieCode.TryParse(_entries[index].CodeText, out var code))
            {
                MarkCodeError(index, InvalidCodeMessage);
                allValid = false;
                continue;
            }

            var key = (code.Address, code.ReplacementValue, code.CompareValue);
            if (!firstIndexByCode.TryAdd(key, index))
            {
                MarkCodeError(firstIndexByCode[key], DuplicateCodeMessage);
                MarkCodeError(index, DuplicateCodeMessage);
                allValid = false;
            }
        }

        if (!allValid)
        {
            ApplyErrorTextBlock.IsVisible = true;
            ApplyErrorTextBlock.Text = DraftCodeErrorMessage;
        }
        else if (
            string.Equals(ApplyErrorTextBlock.Text, DraftCodeErrorMessage, StringComparison.Ordinal)
        )
        {
            ApplyErrorTextBlock.IsVisible = false;
            ApplyErrorTextBlock.Text = string.Empty;
        }

        ApplyButton.IsEnabled = !_isApplying && allValid;
        return allValid;
    }

    private void MarkCodeError(int index, string message)
    {
        var row = _entryRows[index];
        row.CodeEditorBorder.Classes.Add("error");
        row.CodeEditor[property: AutomationProperties.HelpTextProperty] = message;
    }

    private GameGenieCodeEntry[] CreateEntries()
    {
        var entries = new GameGenieCodeEntry[_entries.Count];
        for (var index = 0; index < _entries.Count; index++)
        {
            if (!GameGenieCode.TryParse(_entries[index].CodeText, out var code))
            {
                throw new InvalidOperationException("A Game Genie code draft is invalid.");
            }

            entries[index] = new GameGenieCodeEntry(
                code,
                _entries[index].IsEnabled,
                NormalizeName(_entries[index].Name)
            );
        }

        return entries;
    }

    private void FocusFirstInvalidCode()
    {
        var index = _entryRows.FindIndex(row => row.CodeEditorBorder.Classes.Contains("error"));
        if (index >= 0)
        {
            PostFocus(_entryRows[index].CodeEditor);
        }
    }

    private void RefreshLimitState()
    {
        var reachedLimit = _entries.Count >= GameGenieService.MaxEntryCount;
        var canAdd = !_isApplying && !reachedLimit;
        NameTextBox.IsEnabled = canAdd;
        CodeTextBox.IsEnabled = canAdd;
        AddButton.IsEnabled = canAdd;
        foreach (var row in _entryRows)
        {
            row.CodeEditor.IsEnabled = !_isApplying;
            row.NameEditor.IsEnabled = !_isApplying;
        }

        if (reachedLimit)
        {
            if (!LimitTextBlock.IsVisible)
            {
                LimitTextBlock.IsVisible = true;
                LimitTextBlock.Text = "Maximum of 20 codes reached.";
            }
        }
        else
        {
            LimitTextBlock.IsVisible = false;
            LimitTextBlock.Text = string.Empty;
        }
    }

    private void SetDraftEnabled(bool enabled)
    {
        GameGenieNavButton.IsEnabled = enabled;
        EntriesScrollViewer.IsEnabled = enabled;
        CancelButton.IsEnabled = enabled;
        RefreshLimitState();
        ValidateEntryCodes();
    }

    private void ShowInputError(string message)
    {
        InputErrorTextBlock.IsVisible = true;
        InputErrorTextBlock.Text = message;
    }

    private void HideInputError()
    {
        InputErrorTextBlock.IsVisible = false;
        InputErrorTextBlock.Text = string.Empty;
    }

    private void FocusInitialControl()
    {
        if (NameTextBox.IsEnabled)
        {
            NameTextBox.Focus();
        }
        else if (_entryRows.Count != 0)
        {
            _entryRows[0].Toggle.Focus();
        }
    }

    private void FocusRowAction(int index, RowAction action)
    {
        var row = _entryRows[index];
        PostFocus(
            action switch
            {
                RowAction.MoveUp => row.MoveUp,
                RowAction.MoveDown => row.MoveDown,
                RowAction.Remove => row.Remove,
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            }
        );
    }

    private static void PostFocus(Control control) =>
        Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Input);

    private enum RowAction
    {
        MoveUp = 0,
        MoveDown = 1,
        Remove = 2,
    }

    private sealed class CheatDraft(string codeText, bool isEnabled, string? name)
    {
        public string CodeText { get; set; } = codeText;

        public bool IsEnabled { get; set; } = isEnabled;

        public string? Name { get; set; } = name;
    }

    private readonly record struct EntryRowControls(
        TextBox CodeEditor,
        Border CodeEditorBorder,
        TextBox NameEditor,
        ToggleSwitch Toggle,
        Button MoveUp,
        Button MoveDown,
        Button Remove
    );
}
