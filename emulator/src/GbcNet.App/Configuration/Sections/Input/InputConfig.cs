// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Avalonia.Input;
using GbcNet.App.Input;
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

internal interface IInputBindingConfig
{
    string ButtonName { get; }
    string TargetName { get; }
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

    public static IReadOnlyList<JoypadButton> ButtonsFor<TBinding>()
        where TBinding : struct, Enum =>
        typeof(TBinding) == typeof(Key) ? KeyboardButtons : GamepadButtons;
}

/// <summary>
/// Validates loaded input config before it is converted to runtime bindings.
/// </summary>
internal static class InputConfigValidator
{
    public static bool IsReservedKey(Key key) => key is Key.Space or Key.Tab;

    private static bool TryParseCanonicalName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum =>
        TryParseDefinedName(name, out value)
        && string.Equals(name, Enum.GetName(value), StringComparison.Ordinal);

    private static bool TryParseDefinedName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return !string.IsNullOrWhiteSpace(name)
            && !int.TryParse(name, CultureInfo.InvariantCulture, out _)
            && Enum.TryParse(name, ignoreCase: true, out value)
            && Enum.IsDefined(value);
    }

    public static IReadOnlyList<string> Validate(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        if (config.Version != InputConfig.SupportedVersion)
        {
            errors.Add($"Input config version {config.Version} is not supported.");
        }

        ValidateSection(
            "Keyboard",
            config.Keyboard,
            static c => c.ActiveProfile,
            static c => c.Profiles,
            ValidateKeyboardProfile,
            errors
        );
        ValidateSection(
            "Gamepad",
            config.Gamepad,
            static c => c.ActiveProfile,
            static c => c.Profiles,
            ValidateGamepadProfile,
            errors
        );
        return errors;
    }

    private static void ValidateSection<TConfig, TProfile>(
        string sectionName,
        TConfig? config,
        Func<TConfig, string> getActiveProfile,
        Func<TConfig, IReadOnlyDictionary<string, TProfile>?> getProfiles,
        Action<string, TProfile?, List<string>> validateProfile,
        List<string> errors
    )
        where TConfig : class
        where TProfile : class
    {
        if (config is null)
        {
            errors.Add($"{sectionName} input config is malformed.");
            return;
        }

        ValidateProfiles(
            sectionName: sectionName,
            activeProfile: getActiveProfile(config),
            getProfiles(config),
            (name, profile) => validateProfile(name, profile, errors),
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
    ) =>
        ValidateProfileBindings(
            profileName,
            profile?.Bindings,
            InputConfigMetadata.KeyboardButtons,
            "Keyboard",
            "keyboard key",
            restrictButtonsToAllowed: false,
            binding =>
                TryParseDefinedName(binding.TargetName, out Key key) && key is not Key.None
                    ? (true, key, binding.TargetName)
                    : (false, default, binding.TargetName),
            (key, name) =>
                IsReservedKey(key)
                    ? $"Keyboard profile '{profileName}' uses reserved key '{name}'."
                    : null,
            errors
        );

    private static void ValidateGamepadProfile(
        string profileName,
        GamepadProfileConfig? profile,
        List<string> errors
    ) =>
        ValidateProfileBindings(
            profileName,
            profile?.Bindings,
            InputConfigMetadata.GamepadButtons,
            "Gamepad",
            "control",
            restrictButtonsToAllowed: true,
            binding =>
                TryParseCanonicalName(binding.TargetName, out GamepadButton control)
                && InputConfigMetadata.AllowedGamepadControls.Contains(control)
                    ? (true, control, binding.TargetName)
                    : (false, default, binding.TargetName),
            (_, _) => null,
            errors
        );

    private static void ValidateProfileBindings<TTarget>(
        string profileName,
        IReadOnlyList<IInputBindingConfig>? bindings,
        IReadOnlyList<JoypadButton> allowedButtons,
        string sectionName,
        string targetLabel,
        bool restrictButtonsToAllowed,
        Func<IInputBindingConfig, (bool Ok, TTarget Target, string TargetName)> parseTarget,
        Func<TTarget, string, string?> validateTarget,
        List<string> errors
    )
        where TTarget : struct, Enum
    {
        if (bindings is null)
        {
            errors.Add($"{sectionName} profile '{profileName}' bindings are malformed.");
            return;
        }

        if (bindings.Count != allowedButtons.Count)
        {
            errors.Add(
                $"{sectionName} profile '{profileName}' must contain exactly {allowedButtons.Count} bindings."
            );
        }

        var usedButtons = new HashSet<JoypadButton>();
        var usedTargets = new HashSet<TTarget>();

        foreach (var binding in bindings)
        {
            if (binding is null)
            {
                errors.Add($"{sectionName} profile '{profileName}' contains a malformed binding.");
                continue;
            }

            if (!TryParseCanonicalName(binding.ButtonName, out JoypadButton button))
            {
                errors.Add(
                    $"{sectionName} profile '{profileName}' has an unknown or non-canonical joypad button '{binding.ButtonName}'."
                );
            }
            else if (restrictButtonsToAllowed && !allowedButtons.Contains(button))
            {
                errors.Add(
                    $"{sectionName} profile '{profileName}' cannot bind joypad button '{binding.ButtonName}'."
                );
            }
            else if (!usedButtons.Add(button))
            {
                errors.Add(
                    $"{sectionName} profile '{profileName}' binds joypad button '{binding.ButtonName}' more than once."
                );
            }

            var (ok, target, targetName) = parseTarget(binding);
            if (!ok)
            {
                errors.Add(
                    $"{sectionName} profile '{profileName}' has an unknown or unsupported {targetLabel} '{targetName}'."
                );
                continue;
            }

            var targetError = validateTarget(target, targetName);
            if (targetError is not null)
            {
                errors.Add(targetError);
            }
            else if (!usedTargets.Add(target))
            {
                errors.Add(
                    $"{sectionName} profile '{profileName}' binds {targetLabel} '{targetName}' more than once."
                );
            }
        }

        errors.AddRange(
            allowedButtons
                .Where(button => !usedButtons.Contains(button))
                .Select(button =>
                    $"{sectionName} profile '{profileName}' is missing joypad button '{Enum.GetName(value: button)}'."
                )
        );
    }
}
