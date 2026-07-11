// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

internal sealed class InputConfigDraft
{
    private readonly Dictionary<string, Dictionary<JoypadButton, Key>> _profiles;

    public InputConfigDraft(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = InputConfigValidator.Validate(config);
        if (errors.Count != 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(config));
        }

        _profiles = new Dictionary<string, Dictionary<JoypadButton, Key>>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var (name, profile) in config.Profiles)
        {
            _profiles.Add(name, ToKeyboardMap(profile));
        }

        ActiveProfileName = FindProfileName(config.ActiveProfile);
        SelectedProfileName = ActiveProfileName;
    }

    public string ActiveProfileName { get; private set; }

    public string SelectedProfileName { get; private set; }

    public IReadOnlyList<InputProfileSummary> Profiles =>
        [
            .. _profiles.Keys.Select(name => new InputProfileSummary(
                name,
                string.Equals(name, ActiveProfileName, StringComparison.OrdinalIgnoreCase),
                string.Equals(name, SelectedProfileName, StringComparison.OrdinalIgnoreCase)
            )),
        ];

    public InputEditResult SelectProfile(string? name)
    {
        if (!TryFindProfileName(name, out var existingName))
        {
            return InputEditResult.Fail($"Input profile '{name}' does not exist.");
        }

        SelectedProfileName = existingName;
        return InputEditResult.Success();
    }

    public InputEditResult CreateProfile(string? name)
    {
        var trimmedName = TrimProfileName(name);
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return InputEditResult.Fail("Input profile name must not be blank.");
        }

        if (_profiles.ContainsKey(trimmedName))
        {
            return InputEditResult.Fail($"Input profile '{trimmedName}' already exists.");
        }

        _profiles.Add(
            trimmedName,
            new Dictionary<JoypadButton, Key>(_profiles[SelectedProfileName])
        );
        SelectedProfileName = trimmedName;
        return InputEditResult.Success();
    }

    public InputEditResult RenameProfile(string? currentName, string? newName)
    {
        if (!TryFindProfileName(currentName, out var existingName))
        {
            return InputEditResult.Fail($"Input profile '{currentName}' does not exist.");
        }

        if (
            string.Equals(
                existingName,
                InputConfig.DefaultProfileName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return InputEditResult.Fail("Default input profile cannot be renamed.");
        }

        var trimmedName = TrimProfileName(newName);
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return InputEditResult.Fail("Input profile name must not be blank.");
        }

        if (
            !string.Equals(existingName, trimmedName, StringComparison.OrdinalIgnoreCase)
            && _profiles.ContainsKey(trimmedName)
        )
        {
            return InputEditResult.Fail($"Input profile '{trimmedName}' already exists.");
        }

        var keyboard = _profiles[existingName];
        _profiles.Remove(existingName);
        _profiles.Add(trimmedName, keyboard);

        if (string.Equals(ActiveProfileName, existingName, StringComparison.OrdinalIgnoreCase))
        {
            ActiveProfileName = trimmedName;
        }

        if (string.Equals(SelectedProfileName, existingName, StringComparison.OrdinalIgnoreCase))
        {
            SelectedProfileName = trimmedName;
        }

        return InputEditResult.Success();
    }

    public InputEditResult DeleteProfile(string name)
    {
        if (!TryFindProfileName(name, out var existingName))
        {
            return InputEditResult.Fail($"Input profile '{name}' does not exist.");
        }

        if (
            string.Equals(
                existingName,
                InputConfig.DefaultProfileName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return InputEditResult.Fail("Default input profile cannot be deleted.");
        }

        if (string.Equals(existingName, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return InputEditResult.Fail("Active input profile cannot be deleted.");
        }

        _profiles.Remove(existingName);
        if (string.Equals(SelectedProfileName, existingName, StringComparison.OrdinalIgnoreCase))
        {
            SelectedProfileName = ActiveProfileName;
        }

        return InputEditResult.Success();
    }

    public InputEditResult SetActiveProfile(string name)
    {
        if (!TryFindProfileName(name, out var existingName))
        {
            return InputEditResult.Fail($"Input profile '{name}' does not exist.");
        }

        ActiveProfileName = existingName;
        return InputEditResult.Success();
    }

    public Key GetKeyboardBinding(string profileName, JoypadButton button)
    {
        if (!TryFindProfileName(profileName, out var existingName))
        {
            throw new ArgumentException(
                $"Input profile '{profileName}' does not exist.",
                nameof(profileName)
            );
        }

        if (!InputConfigValidator.RequiredButtons.Contains(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown joypad button.");
        }

        return _profiles[existingName][button];
    }

    public InputEditResult SetKeyboardBinding(string profileName, JoypadButton button, Key key)
    {
        if (!TryFindProfileName(profileName, out var existingName))
        {
            return InputEditResult.Fail($"Input profile '{profileName}' does not exist.");
        }

        if (!InputConfigValidator.RequiredButtons.Contains(button))
        {
            return InputEditResult.Fail($"Unknown joypad button '{button}'.");
        }

        if (!Enum.IsDefined(key) || key is Key.None)
        {
            return InputEditResult.Fail($"Unknown keyboard key '{key}'.");
        }

        if (InputConfigValidator.IsReservedKey(key))
        {
            return InputEditResult.Fail($"Keyboard key '{key}' is reserved.");
        }

        var keyboard = _profiles[existingName];
        if (keyboard.Any(binding => binding.Value == key && binding.Key != button))
        {
            return InputEditResult.Fail($"Keyboard key '{key}' is bound more than once.");
        }

        keyboard[button] = key;
        return InputEditResult.Success();
    }

    public InputConfig Build() =>
        new()
        {
            Version = InputConfig.SupportedVersion,
            ActiveProfile = ActiveProfileName,
            Profiles = new Dictionary<string, InputProfileConfig>(
                _profiles.Select(profile => new KeyValuePair<string, InputProfileConfig>(
                    profile.Key,
                    new InputProfileConfig
                    {
                        Keyboard =
                        [
                            .. InputConfigValidator.RequiredButtons.Select(
                                button => new KeyboardInputBindingConfig(
                                    button.ToString(),
                                    profile.Value[button].ToString()
                                )
                            ),
                        ],
                    }
                )),
                StringComparer.OrdinalIgnoreCase
            ),
        };

    private static string TrimProfileName(string? name) => name?.Trim() ?? string.Empty;

    private bool TryFindProfileName(string? name, out string existingName)
    {
        var trimmedName = name?.Trim();
        var match = string.IsNullOrWhiteSpace(trimmedName)
            ? null
            : _profiles.Keys.FirstOrDefault(profileName =>
                string.Equals(profileName, trimmedName, StringComparison.OrdinalIgnoreCase)
            );

        existingName = match ?? string.Empty;
        return match is not null;
    }

    private string FindProfileName(string name) =>
        TryFindProfileName(name, out var existingName)
            ? existingName
            : throw new InvalidOperationException($"Input profile '{name}' does not exist.");

    private static Dictionary<JoypadButton, Key> ToKeyboardMap(InputProfileConfig profile) =>
        new(
            profile.Keyboard.Select(binding => new KeyValuePair<JoypadButton, Key>(
                Enum.Parse<JoypadButton>(binding.ButtonName, ignoreCase: true),
                Enum.Parse<Key>(binding.KeyName, ignoreCase: true)
            ))
        );
}

internal sealed record InputEditResult(bool Succeeded, string? ErrorMessage)
{
    public static InputEditResult Success() => new(true, null);

    public static InputEditResult Fail(string errorMessage) => new(false, errorMessage);
}

internal sealed record InputProfileSummary(string Name, bool IsActive, bool IsSelected);
