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
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Cheats;

internal sealed partial class CheatsWindow : Window
{
    private const string InvalidGameGenieCodeMessage =
        "Enter a valid 6- or 9-digit Game Genie code.";
    private const string InvalidGameSharkCodeMessage =
        "Enter a valid 8-digit hexadecimal GameShark code.";
    private const string DuplicateCodeMessage = "That code is already in the list.";
    private const string GameGenieDraftCodeErrorMessage =
        "Fix invalid or duplicate Game Genie codes.";
    private const string GameSharkDraftCodeErrorMessage =
        "Fix invalid or duplicate GameShark codes.";
    private const string GameGenieCodeHelpText = "Enter a six- or nine-digit Game Genie code";
    private const string GameSharkCodeHelpText = "Enter an 8-digit hexadecimal GameShark code";

    private readonly List<CheatDraft> _gameGenieEntries;
    private readonly List<CheatDraft> _gameSharkEntries;
    private readonly List<EntryRowControls> _entryRows = [];
    private readonly Func<IReadOnlyList<CheatCodeEntry>, Task> _applyAsync;
    private readonly ILogger _logger;
    private CheatCodeType _currentType;
    private string _gameGenieNameDraft = string.Empty;
    private string _gameGenieCodeDraft = string.Empty;
    private string _gameSharkNameDraft = string.Empty;
    private string _gameSharkCodeDraft = string.Empty;
    private bool _gameGenieEntriesValid = true;
    private bool _gameSharkEntriesValid = true;
    private bool _isApplying;

    internal CheatsWindow(
        IReadOnlyList<CheatCodeEntry> entries,
        Func<IReadOnlyList<CheatCodeEntry>, Task> applyAsync,
        ILogger logger
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(applyAsync);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();

        _gameGenieEntries = new List<CheatDraft>(
            Math.Min(entries.Count, CheatCodeService.MaxEntryCount)
        );
        _gameSharkEntries = new List<CheatDraft>(
            Math.Min(entries.Count, CheatCodeService.MaxEntryCount)
        );
        foreach (var entry in entries)
        {
            var drafts = entry.Code.Type switch
            {
                CheatCodeType.GameGenie => _gameGenieEntries,
                CheatCodeType.GameShark => _gameSharkEntries,
                _ => throw new ArgumentException("Unsupported cheat code type.", nameof(entries)),
            };
            drafts.Add(
                new CheatDraft(entry.Code.CanonicalCode, entry.IsEnabled, NormalizeName(entry.Name))
            );
        }

        _applyAsync = applyAsync;
        _logger = logger;

        Closing += GuardCloseWhileApplying;
        Opened += (_, _) => FocusInitialControl();
        AddHandler(KeyDownEvent, HandleWindowKeyDown, RoutingStrategies.Tunnel);

        UpdatePage();
        RefreshEntries();
    }

    private List<CheatDraft> Entries =>
        _currentType is CheatCodeType.GameGenie ? _gameGenieEntries : _gameSharkEntries;

    private string InvalidCodeMessage =>
        _currentType is CheatCodeType.GameGenie
            ? InvalidGameGenieCodeMessage
            : InvalidGameSharkCodeMessage;

    private string CodeHelpText =>
        _currentType is CheatCodeType.GameGenie ? GameGenieCodeHelpText : GameSharkCodeHelpText;

    private void ShowGameGeniePage(object? sender, RoutedEventArgs e) =>
        ShowPage(CheatCodeType.GameGenie);

    private void ShowGameSharkPage(object? sender, RoutedEventArgs e) =>
        ShowPage(CheatCodeType.GameShark);

    private void ShowPage(CheatCodeType type)
    {
        if (_currentType != type)
        {
            SaveInputDraft();
            _currentType = type;
            UpdatePage();
            RefreshEntries();
        }

        FocusInitialControl();
    }

    private void SaveInputDraft()
    {
        if (_currentType is CheatCodeType.GameGenie)
        {
            _gameGenieNameDraft = NameTextBox.Text ?? string.Empty;
            _gameGenieCodeDraft = CodeTextBox.Text ?? string.Empty;
        }
        else
        {
            _gameSharkNameDraft = NameTextBox.Text ?? string.Empty;
            _gameSharkCodeDraft = CodeTextBox.Text ?? string.Empty;
        }
    }

    private void UpdatePage()
    {
        var gameGenie = _currentType is CheatCodeType.GameGenie;
        GameGenieNavButton.Classes.Set("selected", gameGenie);
        GameSharkNavButton.Classes.Set("selected", !gameGenie);
        PageTitleTextBlock.Text = gameGenie ? "Game Genie" : "GameShark";
        PageHelpTextBlock.Text = gameGenie
            ? "Codes are checked top to bottom; the first matching code wins."
            : "Codes rewrite memory every frame; for the same address, the last code wins.";

        NameTextBox.Text = gameGenie ? _gameGenieNameDraft : _gameSharkNameDraft;
        NameTextBox[property: AutomationProperties.NameProperty] = "Cheat name (optional)";
        NameTextBox[property: AutomationProperties.HelpTextProperty] = gameGenie
            ? "Optional name for the Game Genie code"
            : "Optional name for the GameShark code";

        CodeTextBox.MaxLength = gameGenie ? 32 : 8;
        CodeTextBox.PlaceholderText = gameGenie ? "ABC-DEF or ABC-DEF-GHI" : "01VVLLHH";
        CodeTextBox[property: AutomationProperties.NameProperty] = gameGenie
            ? "Game Genie code"
            : "GameShark code";
        CodeTextBox[property: AutomationProperties.HelpTextProperty] = CodeHelpText;
        CodeTextBox.Text = gameGenie ? _gameGenieCodeDraft : _gameSharkCodeDraft;

        AddButton[property: AutomationProperties.NameProperty] = gameGenie
            ? "Add Game Genie code"
            : "Add GameShark code";
        InputErrorTextBlock[property: AutomationProperties.NameProperty] = gameGenie
            ? "Game Genie code error"
            : "GameShark code error";
        LimitTextBlock[property: AutomationProperties.NameProperty] = gameGenie
            ? "Game Genie code limit"
            : "GameShark code limit";
        EntriesScrollViewer[property: AutomationProperties.NameProperty] = gameGenie
            ? "Game Genie codes"
            : "GameShark codes";
        CounterTextBlock[property: AutomationProperties.NameProperty] = gameGenie
            ? "Game Genie code count"
            : "GameShark code count";

        RefreshCodeInputValidation();
    }

    private void ValidateCodeInput(object? sender, TextChangedEventArgs e) =>
        RefreshCodeInputValidation();

    private void RefreshCodeInputValidation()
    {
        var text = CodeTextBox.Text;
        if (string.IsNullOrEmpty(text) || CheatCode.TryParse(_currentType, text.AsSpan(), out _))
        {
            HideInputError();
            return;
        }

        ShowInputError(InvalidCodeMessage);
    }

    private void AddCode(object? sender, RoutedEventArgs e)
    {
        if (!CheatCode.TryParse(_currentType, CodeTextBox.Text.AsSpan(), out var code))
        {
            ShowInputError(InvalidCodeMessage);
            return;
        }

        if (HasEffectiveDuplicate(code))
        {
            ShowInputError(DuplicateCodeMessage);
            return;
        }

        Entries.Add(
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
        var entries = Entries;
        var newIndex = index + offset;
        var entry = entries[index];
        entries.RemoveAt(index);
        entries.Insert(newIndex, entry);
        RefreshEntries();
        var action = offset < 0 ? RowAction.MoveUp : RowAction.MoveDown;
        if (newIndex == 0)
        {
            action = RowAction.MoveDown;
        }
        else if (newIndex == entries.Count - 1)
        {
            action = RowAction.MoveUp;
        }

        FocusRowAction(newIndex, action);
    }

    private void RemoveEntry(int index)
    {
        var entries = Entries;
        entries.RemoveAt(index);
        RefreshEntries();

        if (entries.Count == 0)
        {
            PostFocus(NameTextBox);
            return;
        }

        FocusRowAction(Math.Min(index, entries.Count - 1), RowAction.Remove);
    }

    private async void ApplyAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!ValidateEntryCodes())
            {
                var currentEntriesValid =
                    _currentType is CheatCodeType.GameGenie
                        ? _gameGenieEntriesValid
                        : _gameSharkEntriesValid;
                var invalidType = _currentType;
                if (currentEntriesValid)
                {
                    invalidType =
                        _currentType is CheatCodeType.GameGenie
                            ? CheatCodeType.GameShark
                            : CheatCodeType.GameGenie;
                }
                if (_currentType != invalidType)
                {
                    SaveInputDraft();
                    _currentType = invalidType;
                    UpdatePage();
                    RefreshEntries();
                }

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
                ShowApplyFailure($"Cheat codes could not be applied: {exception.Message}");
                return;
            }

            _isApplying = false;
            Closing -= GuardCloseWhileApplying;
            Close(dialogResult: true);
        }
        catch (Exception exception)
        {
            CheatsWindowLog.CheatCodeApplyFailed(_logger, exception);
            ShowApplyFailure("Cheat codes could not be applied.");
        }
    }

    private void ShowApplyFailure(string message)
    {
        _isApplying = false;
        ApplyingTextBlock.IsVisible = false;
        ApplyingTextBlock.Text = string.Empty;
        ApplyErrorTextBlock.IsVisible = true;
        ApplyErrorTextBlock.Text = message;
        SetDraftEnabled(enabled: true);
        ApplyButton.Focus();
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
        var entries = Entries;
        EntriesPanel.Children.Clear();
        _entryRows.Clear();

        for (var index = 0; index < entries.Count; index++)
        {
            AddEntryRow(index);
        }

        var hasEntries = entries.Count != 0;
        EmptyStateTextBlock.IsVisible = !hasEntries;
        EntriesScrollViewer.IsVisible = hasEntries;
        CounterTextBlock.Text = $"{entries.Count} / {CheatCodeService.MaxEntryCount}";
        RefreshLimitState();
        ValidateEntryCodes();
    }

    private void AddEntryRow(int index)
    {
        var type = _currentType;
        var entries = Entries;
        var entry = entries[index];
        var gameGenie = type is CheatCodeType.GameGenie;
        var typeName = gameGenie ? "Game Genie" : "GameShark";
        var codeHelpText = gameGenie ? GameGenieCodeHelpText : GameSharkCodeHelpText;
        var codeTextBox = new TextBox
        {
            Classes = { "code-input" },
            MaxLength = gameGenie ? 32 : 8,
            Text = entry.CodeText,
            PlaceholderText = gameGenie ? "ABC-DEF or ABC-DEF-GHI" : "01VVLLHH",
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
            MaxLength = CheatCodeService.MaxNameLength,
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
        moveDownButton.IsEnabled = index != entries.Count - 1;
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
            var context = $"entry {index + 1}";
            if (CheatCode.TryParse(type, entry.CodeText.AsSpan(), out var code))
            {
                context = code.CanonicalCode;
            }

            codeTextBox[property: AutomationProperties.NameProperty] = $"{typeName} code {context}";
            nameTextBox[property: AutomationProperties.NameProperty] = $"Name for {context}";
            toggle[property: AutomationProperties.NameProperty] = $"Enable {context}";
            moveUpButton[property: AutomationProperties.NameProperty] = $"Move {context} up";
            moveDownButton[property: AutomationProperties.NameProperty] = $"Move {context} down";
            removeButton[property: AutomationProperties.NameProperty] = $"Remove {context}";
        }

        codeTextBox[property: AutomationProperties.HelpTextProperty] = codeHelpText;
        codeTextBox.TextChanged += (_, _) =>
        {
            entry.CodeText = codeTextBox.Text ?? string.Empty;
            UpdateAutomationNames();
            ValidateEntryCodes();
        };
        nameTextBox[property: AutomationProperties.HelpTextProperty] =
            $"Optional name for this {typeName} code";
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
                    index == entries.Count - 1
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

    private static bool IsEffectiveDuplicate(CheatCode left, CheatCode right) =>
        left.Type == right.Type
        && left.Address == right.Address
        && left.Value == right.Value
        && left.CompareValue == right.CompareValue;

    private bool HasEffectiveDuplicate(CheatCode code)
    {
        foreach (var entry in Entries)
        {
            if (
                CheatCode.TryParse(_currentType, entry.CodeText.AsSpan(), out var existing)
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
            row.CodeEditor[property: AutomationProperties.HelpTextProperty] = CodeHelpText;
        }

        _gameGenieEntriesValid = ValidateEntries(
            _gameGenieEntries,
            CheatCodeType.GameGenie,
            markErrors: _currentType is CheatCodeType.GameGenie
        );
        _gameSharkEntriesValid = ValidateEntries(
            _gameSharkEntries,
            CheatCodeType.GameShark,
            markErrors: _currentType is CheatCodeType.GameShark
        );

        var allValid = _gameGenieEntriesValid && _gameSharkEntriesValid;
        if (!allValid)
        {
            ApplyErrorTextBlock.IsVisible = true;
            var showGameGenieError =
                !_gameGenieEntriesValid
                && (_currentType is CheatCodeType.GameGenie || _gameSharkEntriesValid);
            ApplyErrorTextBlock.Text = showGameGenieError
                ? GameGenieDraftCodeErrorMessage
                : GameSharkDraftCodeErrorMessage;
        }
        else if (IsDraftCodeError(ApplyErrorTextBlock.Text))
        {
            ApplyErrorTextBlock.IsVisible = false;
            ApplyErrorTextBlock.Text = string.Empty;
        }

        ApplyButton.IsEnabled = !_isApplying && allValid;
        return allValid;
    }

    private bool ValidateEntries(
        IReadOnlyList<CheatDraft> entries,
        CheatCodeType type,
        bool markErrors
    )
    {
        var firstIndexByCode =
            new Dictionary<(ushort Address, byte Value, byte? CompareValue), int>();
        var allValid = true;
        var invalidCodeMessage =
            type is CheatCodeType.GameGenie
                ? InvalidGameGenieCodeMessage
                : InvalidGameSharkCodeMessage;

        for (var index = 0; index < entries.Count; index++)
        {
            if (!CheatCode.TryParse(type, entries[index].CodeText.AsSpan(), out var code))
            {
                if (markErrors)
                {
                    MarkCodeError(index, invalidCodeMessage);
                }

                allValid = false;
                continue;
            }

            var key = (code.Address, code.Value, code.CompareValue);
            if (!firstIndexByCode.TryAdd(key, index))
            {
                if (markErrors)
                {
                    MarkCodeError(firstIndexByCode[key], DuplicateCodeMessage);
                    MarkCodeError(index, DuplicateCodeMessage);
                }

                allValid = false;
            }
        }

        return allValid;
    }

    private static bool IsDraftCodeError(string? message) =>
        string.Equals(message, GameGenieDraftCodeErrorMessage, StringComparison.Ordinal)
        || string.Equals(message, GameSharkDraftCodeErrorMessage, StringComparison.Ordinal);

    private void MarkCodeError(int index, string message)
    {
        var row = _entryRows[index];
        row.CodeEditorBorder.Classes.Add("error");
        row.CodeEditor[property: AutomationProperties.HelpTextProperty] = message;
    }

    private CheatCodeEntry[] CreateEntries()
    {
        var entries = new CheatCodeEntry[_gameGenieEntries.Count + _gameSharkEntries.Count];
        FillEntries(_gameGenieEntries, CheatCodeType.GameGenie, entries);
        FillEntries(
            _gameSharkEntries,
            CheatCodeType.GameShark,
            entries.AsSpan(_gameGenieEntries.Count)
        );
        return entries;
    }

    private static void FillEntries(
        IReadOnlyList<CheatDraft> drafts,
        CheatCodeType type,
        Span<CheatCodeEntry> entries
    )
    {
        for (var index = 0; index < drafts.Count; index++)
        {
            if (!CheatCode.TryParse(type, drafts[index].CodeText.AsSpan(), out var code))
            {
                var typeName = type is CheatCodeType.GameGenie ? "Game Genie" : "GameShark";
                throw new InvalidOperationException($"A {typeName} code draft is invalid.");
            }

            entries[index] = new CheatCodeEntry(
                code,
                drafts[index].IsEnabled,
                NormalizeName(drafts[index].Name)
            );
        }
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
        var reachedLimit = Entries.Count >= CheatCodeService.MaxEntryCount;
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
        GameSharkNavButton.IsEnabled = enabled;
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
            PostFocus(NameTextBox);
        }
        else if (_entryRows.Count != 0)
        {
            PostFocus(_entryRows[0].Toggle);
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

internal static partial class CheatsWindowLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Cheat codes could not be applied.")]
    internal static partial void CheatCodeApplyFailed(ILogger logger, Exception exception);
}
