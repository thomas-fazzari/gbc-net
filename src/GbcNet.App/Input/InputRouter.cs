// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// Aggregates physical input states before updating emulated joypad buttons.
/// </summary>
internal sealed class InputRouter(
    IReadOnlyList<InputBinding> keyboardBindings,
    IReadOnlyList<GamepadBinding> gamepadBindings,
    Action<JoypadButton, bool> setButtonState
)
{
    private readonly HashSet<Key> _activeKeys = [];
    private readonly Dictionary<uint, HashSet<GamepadButton>> _activeGamepadButtonsByDevice = [];
    private readonly Dictionary<uint, HashSet<JoypadButton>> _activeGamepadDirectionsByDevice = [];
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

    public bool ApplyGamepadButton(uint deviceId, GamepadButton control, bool pressed)
    {
        if (!_buttonByGamepadControl.TryGetValue(control, out var button))
        {
            return false;
        }

        if (pressed)
        {
            var activeButtons = GetActiveGamepadButtons(deviceId);

            if (activeButtons.Add(control))
            {
                UpdateButton(button, pressed: true);
            }
        }
        else if (
            _activeGamepadButtonsByDevice.TryGetValue(deviceId, out var activeButtons)
            && activeButtons.Remove(control)
        )
        {
            UpdateButton(button, pressed: false);

            if (activeButtons.Count == 0)
            {
                _activeGamepadButtonsByDevice.Remove(deviceId);
            }
        }

        return true;
    }

    public bool ApplyGamepadDirection(uint deviceId, JoypadButton button, bool pressed)
    {
        if (
            button
            is not (JoypadButton.Up or JoypadButton.Down or JoypadButton.Left or JoypadButton.Right)
        )
        {
            return false;
        }

        if (pressed)
        {
            var activeDirections = GetActiveGamepadDirections(deviceId);

            if (activeDirections.Add(button))
            {
                UpdateButton(button, pressed: true);
            }
        }
        else if (
            _activeGamepadDirectionsByDevice.TryGetValue(deviceId, out var activeDirections)
            && activeDirections.Remove(button)
        )
        {
            UpdateButton(button, pressed: false);

            if (activeDirections.Count == 0)
            {
                _activeGamepadDirectionsByDevice.Remove(deviceId);
            }
        }

        return true;
    }

    public void ReleaseGamepad(uint deviceId)
    {
        if (_activeGamepadButtonsByDevice.Remove(deviceId, out var activeButtons))
        {
            foreach (var control in activeButtons)
            {
                UpdateButton(_buttonByGamepadControl[control], pressed: false);
            }
        }

        if (_activeGamepadDirectionsByDevice.Remove(deviceId, out var activeDirections))
        {
            foreach (var button in activeDirections)
            {
                UpdateButton(button, pressed: false);
            }
        }
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
        _activeGamepadButtonsByDevice.Clear();
        _activeGamepadDirectionsByDevice.Clear();

        foreach (var button in Enum.GetValues<JoypadButton>())
        {
            if (_activeInputCountByButton[button] > 0)
            {
                setButtonState(button, false);
            }

            _activeInputCountByButton[button] = 0;
        }
    }

    private void UpdateButton(JoypadButton button, bool pressed)
    {
        var activeInputCount = _activeInputCountByButton[button];
        var nextActiveInputCount = pressed ? activeInputCount + 1 : activeInputCount - 1;
        _activeInputCountByButton[button] = nextActiveInputCount;

        if (activeInputCount == 0 || nextActiveInputCount == 0)
        {
            setButtonState(button, nextActiveInputCount > 0);
        }
    }

    private HashSet<GamepadButton> GetActiveGamepadButtons(uint deviceId)
    {
        if (!_activeGamepadButtonsByDevice.TryGetValue(deviceId, out var activeButtons))
        {
            activeButtons = [];
            _activeGamepadButtonsByDevice.Add(deviceId, activeButtons);
        }

        return activeButtons;
    }

    private HashSet<JoypadButton> GetActiveGamepadDirections(uint deviceId)
    {
        if (!_activeGamepadDirectionsByDevice.TryGetValue(deviceId, out var activeDirections))
        {
            activeDirections = [];
            _activeGamepadDirectionsByDevice.Add(deviceId, activeDirections);
        }

        return activeDirections;
    }

    private static Dictionary<Key, JoypadButton> CreateKeyboardLookup(
        IReadOnlyList<InputBinding> bindings
    ) => bindings.ToDictionary(binding => binding.Key, binding => binding.Button);

    private static Dictionary<GamepadButton, JoypadButton> CreateGamepadLookup(
        IReadOnlyList<GamepadBinding> bindings
    ) => bindings.ToDictionary(binding => binding.Control, binding => binding.Button);

    private static Dictionary<JoypadButton, int> CreateButtonCounts() =>
        Enum.GetValues<JoypadButton>().ToDictionary(button => button, static _ => 0);
}
