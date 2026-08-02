// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Input;
using GbcNet.App.Utils;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Strongly typed input configuration loaded from defaults or a user config file.
/// </summary>
internal sealed class InputConfig
{
    public const int SupportedVersion = 2;
    public const string DefaultProfileName = "default";

    public int Version { get; set; }

    public KeyboardInputConfig Keyboard { get; set; } = null!;

    public GamepadInputConfig Gamepad { get; set; } = null!;
}

internal static class InputConfigMetadata
{
    public static readonly IReadOnlyList<JoypadButton> KeyboardButtons =
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

    public static readonly IReadOnlyList<JoypadButton> GamepadButtons =
    [
        JoypadButton.A,
        JoypadButton.B,
        JoypadButton.Start,
        JoypadButton.Select,
    ];

    public static readonly IReadOnlySet<GamepadButton> AllowedGamepadControls =
        new HashSet<GamepadButton>
        {
            GamepadButton.South,
            GamepadButton.East,
            GamepadButton.West,
            GamepadButton.North,
            GamepadButton.Back,
            GamepadButton.Start,
            GamepadButton.LeftStick,
            GamepadButton.RightStick,
            GamepadButton.LeftShoulder,
            GamepadButton.RightShoulder,
        };
}

/// <summary>
/// Validates loaded input config before it is converted to runtime bindings.
/// </summary>
internal static class InputConfigValidator
{
    public static bool IsReservedKey(Key key) => key is Key.Space or Key.Tab;

    public static IReadOnlyList<string> Validate(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        if (config.Version != InputConfig.SupportedVersion)
        {
            errors.Add($"Input config version {config.Version} is not supported.");
        }

        ValidateKeyboard(config.Keyboard, errors);
        ValidateGamepad(config.Gamepad, errors);

        return errors;
    }

    private static void ValidateKeyboard(KeyboardInputConfig? config, List<string> errors)
    {
        if (config is null)
        {
            errors.Add("Keyboard input config is malformed.");
            return;
        }

        ValidateProfiles(
            sectionName: "Keyboard",
            activeProfile: config.ActiveProfile,
            config.Profiles,
            (name, profile) => ValidateKeyboardProfile(name, profile, errors),
            errors
        );
    }

    private static void ValidateGamepad(GamepadInputConfig? config, List<string> errors)
    {
        if (config is null)
        {
            errors.Add("Gamepad input config is malformed.");
            return;
        }

        ValidateProfiles(
            sectionName: "Gamepad",
            activeProfile: config.ActiveProfile,
            config.Profiles,
            (name, profile) => ValidateGamepadProfile(name, profile, errors),
            errors
        );
    }

    private static void ValidateProfiles<TProfile>(
        string sectionName,
        string? activeProfile,
        IReadOnlyDictionary<string, TProfile>? profiles,
        Action<string, TProfile?> validateProfile,
        List<string> errors
    )
        where TProfile : class
    {
        if (profiles is null)
        {
            errors.Add($"{sectionName} input config must contain at least one profile.");
            return;
        }

        if (profiles.Count == 0)
        {
            errors.Add($"{sectionName} input config must contain at least one profile.");
        }

        var profileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDefaultProfile = false;
        var hasActiveProfile = false;
        var trimmedActiveProfile = activeProfile?.Trim();

        foreach (var (name, profile) in profiles)
        {
            var trimmedName = name?.Trim();

            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                errors.Add($"{sectionName} profile name must not be blank.");
                continue;
            }

            if (!string.Equals(name, trimmedName, comparisonType: StringComparison.Ordinal))
            {
                errors.Add($"{sectionName} profile name '{name}' must be trimmed.");
            }

            if (!profileNames.Add(trimmedName))
            {
                errors.Add($"{sectionName} profile name '{trimmedName}' is used more than once.");
            }

            hasDefaultProfile |= string.Equals(
                trimmedName,
                InputConfig.DefaultProfileName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            );
            hasActiveProfile |= string.Equals(
                trimmedName,
                trimmedActiveProfile,
                comparisonType: StringComparison.OrdinalIgnoreCase
            );

            validateProfile(trimmedName, profile);
        }

        if (!hasDefaultProfile)
        {
            errors.Add(
                $"{sectionName} input config must contain protected '{InputConfig.DefaultProfileName}' profile."
            );
        }

        if (string.IsNullOrWhiteSpace(activeProfile))
        {
            errors.Add($"{sectionName} active input profile must not be blank.");
        }
        else if (
            !string.Equals(
                activeProfile,
                trimmedActiveProfile,
                comparisonType: StringComparison.Ordinal
            )
        )
        {
            errors.Add($"{sectionName} active input profile '{activeProfile}' must be trimmed.");
        }
        else if (!hasActiveProfile)
        {
            errors.Add($"{sectionName} profile '{activeProfile}' does not exist.");
        }
    }

    private static void ValidateKeyboardProfile(
        string profileName,
        KeyboardProfileConfig? profile,
        List<string> errors
    )
    {
        if (profile?.Bindings is null)
        {
            errors.Add($"Keyboard profile '{profileName}' bindings are malformed.");
            return;
        }

        if (profile.Bindings.Count != InputConfigMetadata.KeyboardButtons.Count)
        {
            errors.Add(
                $"Keyboard profile '{profileName}' must contain exactly {InputConfigMetadata.KeyboardButtons.Count} bindings."
            );
        }

        var usedButtons = new HashSet<JoypadButton>();
        var usedKeys = new HashSet<Key>();

        foreach (var binding in profile.Bindings)
        {
            if (binding is null)
            {
                errors.Add($"Keyboard profile '{profileName}' contains a malformed binding.");
                continue;
            }

            if (!EnumParser.TryParseCanonicalName(binding.ButtonName, out JoypadButton button))
            {
                errors.Add(
                    $"Keyboard profile '{profileName}' has an unknown or non-canonical joypad button '{binding.ButtonName}'."
                );
            }
            else if (!usedButtons.Add(button))
            {
                errors.Add(
                    $"Keyboard profile '{profileName}' binds joypad button '{binding.ButtonName}' more than once."
                );
            }

            if (!EnumParser.TryParseDefinedName(binding.KeyName, out Key key) || key is Key.None)
            {
                errors.Add(
                    $"Keyboard profile '{profileName}' has an unknown keyboard key '{binding.KeyName}'."
                );
                continue;
            }

            if (IsReservedKey(key))
            {
                errors.Add(
                    $"Keyboard profile '{profileName}' uses reserved key '{binding.KeyName}'."
                );
            }
            else if (!usedKeys.Add(key))
            {
                errors.Add(
                    $"Keyboard profile '{profileName}' binds keyboard key '{binding.KeyName}' more than once."
                );
            }
        }

        errors.AddRange(
            InputConfigMetadata
                .KeyboardButtons.Where(button => !usedButtons.Contains(button))
                .Select(button =>
                    $"Keyboard profile '{profileName}' is missing joypad button '{Enum.GetName(value: button)}'."
                )
        );
    }

    private static void ValidateGamepadProfile(
        string profileName,
        GamepadProfileConfig? profile,
        List<string> errors
    )
    {
        if (profile?.Bindings is null)
        {
            errors.Add($"Gamepad profile '{profileName}' bindings are malformed.");
            return;
        }

        if (profile.Bindings.Count != InputConfigMetadata.GamepadButtons.Count)
        {
            errors.Add(
                $"Gamepad profile '{profileName}' must contain exactly {InputConfigMetadata.GamepadButtons.Count} bindings."
            );
        }

        var usedButtons = new HashSet<JoypadButton>();
        var usedControls = new HashSet<GamepadButton>();

        foreach (var binding in profile.Bindings)
        {
            if (binding is null)
            {
                errors.Add($"Gamepad profile '{profileName}' contains a malformed binding.");
                continue;
            }

            if (!EnumParser.TryParseCanonicalName(binding.ButtonName, out JoypadButton button))
            {
                errors.Add(
                    $"Gamepad profile '{profileName}' has an unknown or non-canonical joypad button '{binding.ButtonName}'."
                );
            }
            else if (!InputConfigMetadata.GamepadButtons.Contains(button))
            {
                errors.Add(
                    $"Gamepad profile '{profileName}' cannot bind joypad button '{binding.ButtonName}'."
                );
            }
            else if (!usedButtons.Add(button))
            {
                errors.Add(
                    $"Gamepad profile '{profileName}' binds joypad button '{binding.ButtonName}' more than once."
                );
            }

            if (
                !EnumParser.TryParseCanonicalName(binding.ControlName, out GamepadButton control)
                || !InputConfigMetadata.AllowedGamepadControls.Contains(control)
            )
            {
                errors.Add(
                    $"Gamepad profile '{profileName}' has an unknown or unsupported control '{binding.ControlName}'."
                );
            }
            else if (!usedControls.Add(control))
            {
                errors.Add(
                    $"Gamepad profile '{profileName}' binds control '{binding.ControlName}' more than once."
                );
            }
        }

        errors.AddRange(
            InputConfigMetadata
                .GamepadButtons.Where(button => !usedButtons.Contains(button))
                .Select(button =>
                    $"Gamepad profile '{profileName}' is missing joypad button '{Enum.GetName(value: button)}'."
                )
        );
    }
}
