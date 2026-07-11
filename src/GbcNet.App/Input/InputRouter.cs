// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// Aggregates physical input states before updating emulated joypad buttons.
/// </summary>
internal sealed class InputRouter(
    IReadOnlyList<InputBinding> bindings,
    Action<JoypadButton, bool> setButtonState
)
{
    private readonly HashSet<Key> _activeKeys = [];
    private readonly Dictionary<JoypadButton, int> _activeInputCountByButton = CreateButtonCounts();
    private Dictionary<Key, JoypadButton> _buttonByKey = CreateButtonLookup(bindings);

    public bool Apply(Key key, bool pressed)
    {
        if (!_buttonByKey.TryGetValue(key, out var button))
        {
            return false;
        }

        var stateChanged = pressed ? _activeKeys.Add(key) : _activeKeys.Remove(key);

        if (!stateChanged)
        {
            return true;
        }

        var activeInputCount = _activeInputCountByButton[button];
        var nextActiveInputCount = pressed ? activeInputCount + 1 : activeInputCount - 1;
        _activeInputCountByButton[button] = nextActiveInputCount;

        if (activeInputCount == 0 || nextActiveInputCount == 0)
        {
            setButtonState(button, nextActiveInputCount > 0);
        }

        return true;
    }

    public void ReplaceBindings(IReadOnlyList<InputBinding> bindings)
    {
        var replacement = CreateButtonLookup(bindings);

        Clear();
        _buttonByKey = replacement;
    }

    public void Clear()
    {
        _activeKeys.Clear();

        foreach (var button in Enum.GetValues<JoypadButton>())
        {
            if (_activeInputCountByButton[button] > 0)
            {
                setButtonState(button, false);
            }

            _activeInputCountByButton[button] = 0;
        }
    }

    private static Dictionary<Key, JoypadButton> CreateButtonLookup(
        IReadOnlyList<InputBinding> bindings
    ) => bindings.ToDictionary(binding => binding.Key, binding => binding.Button);

    private static Dictionary<JoypadButton, int> CreateButtonCounts() =>
        Enum.GetValues<JoypadButton>().ToDictionary(button => button, static _ => 0);
}
