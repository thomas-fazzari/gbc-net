// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// User-editable input bindings loaded from defaults or configuration.
/// </summary>
internal sealed class InputMap(
    IReadOnlyList<InputBinding> keyboardBindings,
    IReadOnlyList<GamepadBinding> gamepadBindings
)
{
    public IReadOnlyList<InputBinding> KeyboardBindings { get; } = keyboardBindings;
    public IReadOnlyList<GamepadBinding> GamepadBindings { get; } = gamepadBindings;

    public static InputMap FromConfig(InputConfig config)
    {
        var keyboardProfile = config.Keyboard.Profiles.TryGetValue(
            config.Keyboard.ActiveProfile,
            out var exactKeyboardProfile
        )
            ? exactKeyboardProfile
            : config
                .Keyboard.Profiles.First(profile =>
                    string.Equals(
                        a: profile.Key,
                        b: config.Keyboard.ActiveProfile,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                )
                .Value;

        var gamepadProfile = config.Gamepad.Profiles.TryGetValue(
            config.Gamepad.ActiveProfile,
            out var exactGamepadProfile
        )
            ? exactGamepadProfile
            : config
                .Gamepad.Profiles.First(profile =>
                    string.Equals(
                        a: profile.Key,
                        b: config.Gamepad.ActiveProfile,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                )
                .Value;

        return new InputMap(
            [
                .. keyboardProfile.Bindings.Select(binding => new InputBinding(
                    Enum.Parse<Key>(binding.KeyName),
                    Enum.Parse<JoypadButton>(binding.ButtonName, ignoreCase: true)
                )),
            ],
            [
                .. gamepadProfile.Bindings.Select(binding => new GamepadBinding(
                    Enum.Parse<GamepadButton>(binding.ControlName, ignoreCase: true),
                    Enum.Parse<JoypadButton>(binding.ButtonName, ignoreCase: true)
                )),
            ]
        );
    }
}
