// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// Aggregates keyboard and gamepad contributions before updating emulated joypad buttons.
/// </summary>
internal sealed class InputRouter(
    IReadOnlyList<InputBinding> keyboardBindings,
    IReadOnlyList<GamepadBinding> gamepadBindings,
    Action<JoypadButton, bool> setButtonState
)
{
    private readonly HashSet<Key> _activeKeys = [];
    private readonly Dictionary<JoypadButton, int> _activeInputCountByButton = CreateButtonCounts();
    private Dictionary<Key, JoypadButton> _buttonByKey = CreateKeyboardLookup(keyboardBindings);
    private Dictionary<GamepadButton, JoypadButton> _buttonByGamepadControl = CreateGamepadLookup(
        gamepadBindings
    );

    public bool Apply(Key key, bool pressed)
    {
        if (!_buttonByKey.TryGetValue(key, out var button))
        {
            return false;
        }

        if (pressed ? _activeKeys.Add(key) : _activeKeys.Remove(key))
        {
            UpdateButton(button, pressed);
        }

        return true;
    }

    public bool ApplyGamepadButton(GamepadButton control, bool pressed)
    {
        if (!_buttonByGamepadControl.TryGetValue(control, out var button))
        {
            return false;
        }

        UpdateButton(button, pressed);
        return true;
    }

    public bool ApplyGamepadDirection(JoypadButton button, bool pressed)
    {
        if (
            button
            is not (JoypadButton.Up or JoypadButton.Down or JoypadButton.Left or JoypadButton.Right)
        )
        {
            return false;
        }

        UpdateButton(button, pressed);
        return true;
    }

    public void ReplaceBindings(
        IReadOnlyList<InputBinding> keyboardBindings,
        IReadOnlyList<GamepadBinding> gamepadBindings
    )
    {
        var replacementKeyboardLookup = CreateKeyboardLookup(keyboardBindings);
        var replacementGamepadLookup = CreateGamepadLookup(gamepadBindings);

        Clear();
        _buttonByKey = replacementKeyboardLookup;
        _buttonByGamepadControl = replacementGamepadLookup;
    }

    public void Clear()
    {
        _activeKeys.Clear();

        foreach (var button in Enum.GetValues<JoypadButton>())
        {
            if (_activeInputCountByButton[button] > 0)
            {
                setButtonState(button, arg2: false);
            }

            _activeInputCountByButton[button] = 0;
        }
    }

    private void UpdateButton(JoypadButton button, bool pressed)
    {
        var activeInputCount = _activeInputCountByButton[button];

        if (!pressed && activeInputCount == 0)
        {
            return;
        }

        var nextActiveInputCount = pressed ? activeInputCount + 1 : activeInputCount - 1;
        _activeInputCountByButton[button] = nextActiveInputCount;

        if (activeInputCount == 0 || nextActiveInputCount == 0)
        {
            setButtonState(button, nextActiveInputCount > 0);
        }
    }

    private static Dictionary<Key, JoypadButton> CreateKeyboardLookup(
        IReadOnlyList<InputBinding> bindings
    ) =>
        bindings.ToDictionary(
            keySelector: binding => binding.Key,
            elementSelector: binding => binding.Button
        );

    private static Dictionary<GamepadButton, JoypadButton> CreateGamepadLookup(
        IReadOnlyList<GamepadBinding> bindings
    ) =>
        bindings.ToDictionary(
            keySelector: binding => binding.Control,
            elementSelector: binding => binding.Button
        );

    private static Dictionary<JoypadButton, int> CreateButtonCounts() =>
        Enum.GetValues<JoypadButton>()
            .ToDictionary(keySelector: button => button, elementSelector: static _ => 0);
}
