// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Avalonia.Input;
using GbcNet.App.Utils;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Strongly typed input configuration loaded from defaults or a user config file.
/// </summary>
internal sealed class InputConfig
{
    /// <summary>
    /// Supported input configuration schema version.
    /// </summary>
    public const int SupportedVersion = 1;
    public const string DefaultProfileName = "default";

    public int Version { get; set; } = SupportedVersion;

    /// <summary>
    /// Profile activated on startup.
    /// </summary>
    public string ActiveProfile { get; set; } = DefaultProfileName;

    /// <summary>
    /// Available input profiles keyed by profile name.
    /// </summary>
    public IReadOnlyDictionary<string, InputProfileConfig> Profiles { get; set; } =
        new Dictionary<string, InputProfileConfig>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Validates loaded input config before it is converted to runtime bindings.
/// </summary>
internal static class InputConfigValidator
{
    public static IReadOnlyList<JoypadButton> RequiredButtons { get; } =
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

    public static bool IsReservedKey(Key key) => key is Key.Space or Key.Tab;

    public static IReadOnlyList<string> Validate(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        if (config.Version != InputConfig.SupportedVersion)
        {
            errors.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Input config version {config.Version} is not supported."
                )
            );
        }

        if (config.Profiles is null)
        {
            errors.Add("Input config must contain at least one profile.");
            return errors;
        }

        if (config.Profiles.Count == 0)
        {
            errors.Add("Input config must contain at least one profile.");
        }

        var profileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDefaultProfile = false;
        var hasActiveProfile = false;

        foreach (var (name, profile) in config.Profiles)
        {
            var trimmedName = name?.Trim();

            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                errors.Add("Input profile name must not be blank.");
                continue;
            }

            if (!string.Equals(name, trimmedName, StringComparison.Ordinal))
            {
                errors.Add($"Input profile name '{name}' must be trimmed.");
            }

            if (!profileNames.Add(trimmedName))
            {
                errors.Add($"Input profile name '{trimmedName}' is used more than once.");
            }

            if (
                string.Equals(
                    trimmedName,
                    InputConfig.DefaultProfileName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                hasDefaultProfile = true;
            }

            if (
                string.Equals(
                    trimmedName,
                    config.ActiveProfile?.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                hasActiveProfile = true;
            }

            ValidateProfile(trimmedName, profile, errors);
        }

        if (!hasDefaultProfile)
        {
            errors.Add(
                $"Input config must contain protected '{InputConfig.DefaultProfileName}' profile."
            );
        }

        if (string.IsNullOrWhiteSpace(config.ActiveProfile))
        {
            errors.Add("Active input profile must not be blank.");
        }
        else if (!hasActiveProfile)
        {
            errors.Add($"Input profile '{config.ActiveProfile}' does not exist.");
        }

        return errors;
    }

    private static void ValidateProfile(
        string profileName,
        InputProfileConfig? profile,
        List<string> errors
    )
    {
        if (profile is null)
        {
            errors.Add($"Input profile '{profileName}' is malformed.");
            return;
        }

        if (profile.Keyboard is null)
        {
            errors.Add($"Input profile '{profileName}' keyboard bindings are malformed.");
            return;
        }

        if (profile.Keyboard.Count != RequiredButtons.Count)
        {
            errors.Add(
                $"Input profile '{profileName}' must contain exactly {RequiredButtons.Count} keyboard bindings."
            );
        }

        var usedButtons = new HashSet<JoypadButton>();
        var usedKeys = new HashSet<Key>();

        foreach (var binding in profile.Keyboard)
        {
            if (binding is null)
            {
                errors.Add($"Input profile '{profileName}' contains a malformed keyboard binding.");
                continue;
            }

            if (!EnumParser.TryParseDefinedName(binding.ButtonName, out JoypadButton button))
            {
                errors.Add($"Unknown joypad button '{binding.ButtonName}'.");
            }
            else if (!usedButtons.Add(button))
            {
                errors.Add($"Joypad button '{binding.ButtonName}' is bound more than once.");
            }

            if (!EnumParser.TryParseDefinedName(binding.KeyName, out Key key) || key is Key.None)
            {
                errors.Add($"Unknown keyboard key '{binding.KeyName}'.");
                continue;
            }

            if (IsReservedKey(key))
            {
                errors.Add($"Keyboard key '{binding.KeyName}' is reserved.");
            }
            else if (!usedKeys.Add(key))
            {
                errors.Add($"Keyboard key '{binding.KeyName}' is bound more than once.");
            }
        }

        foreach (var button in RequiredButtons.Where(button => !usedButtons.Contains(button)))
        {
            errors.Add($"Input profile '{profileName}' is missing joypad button '{button}'.");
        }
    }
}
