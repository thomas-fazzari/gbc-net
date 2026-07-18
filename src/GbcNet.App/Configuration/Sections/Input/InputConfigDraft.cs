// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

internal sealed class InputConfigDraft
{
    private readonly Dictionary<string, Dictionary<JoypadButton, Key>> _keyboardProfiles;
    private readonly Dictionary<string, Dictionary<JoypadButton, GamepadButton>> _gamepadProfiles;

    public InputConfigDraft(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = InputConfigValidator.Validate(config);
        if (errors.Count != 0)
        {
            throw new ArgumentException(
                message: string.Join(Environment.NewLine, errors),
                paramName: nameof(config)
            );
        }

        _keyboardProfiles = new Dictionary<string, Dictionary<JoypadButton, Key>>(
            StringComparer.OrdinalIgnoreCase
        );
        _gamepadProfiles = new Dictionary<string, Dictionary<JoypadButton, GamepadButton>>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var (name, profile) in config.Keyboard.Profiles)
        {
            _keyboardProfiles.Add(name, ToKeyboardMap(profile));
        }

        foreach (var (name, profile) in config.Gamepad.Profiles)
        {
            _gamepadProfiles.Add(name, ToGamepadMap(profile));
        }

        ActiveKeyboardProfileName = FindProfileName(
            _keyboardProfiles,
            name: config.Keyboard.ActiveProfile,
            sectionName: "Keyboard"
        );
        SelectedKeyboardProfileName = ActiveKeyboardProfileName;
        ActiveGamepadProfileName = FindProfileName(
            _gamepadProfiles,
            name: config.Gamepad.ActiveProfile,
            sectionName: "Gamepad"
        );
        SelectedGamepadProfileName = ActiveGamepadProfileName;
    }

    public string ActiveKeyboardProfileName { get; private set; }

    public string SelectedKeyboardProfileName { get; private set; }

    public string ActiveGamepadProfileName { get; private set; }

    public string SelectedGamepadProfileName { get; private set; }

    public IReadOnlyList<InputProfileSummary> KeyboardProfiles =>
        GetProfileSummaries(
            _keyboardProfiles,
            activeProfileName: ActiveKeyboardProfileName,
            selectedProfileName: SelectedKeyboardProfileName
        );

    public IReadOnlyList<InputProfileSummary> GamepadProfiles =>
        GetProfileSummaries(
            _gamepadProfiles,
            activeProfileName: ActiveGamepadProfileName,
            selectedProfileName: SelectedGamepadProfileName
        );

    public IReadOnlyList<JoypadButton> KeyboardBindingConflicts =>
        GetKeyboardBindingConflicts(SelectedKeyboardProfileName);

    public IReadOnlyList<JoypadButton> GamepadBindingConflicts =>
        GetGamepadBindingConflicts(SelectedGamepadProfileName);

    public InputEditResult SelectKeyboardProfile(string? name) =>
        SelectProfile(
            _keyboardProfiles,
            name: name,
            setSelectedName: selectedName => SelectedKeyboardProfileName = selectedName,
            sectionName: "Keyboard"
        );

    public InputEditResult CreateKeyboardProfile(string? name) =>
        CreateProfile(
            _keyboardProfiles,
            name: name,
            selectedName: SelectedKeyboardProfileName,
            setSelectedName: selectedName => SelectedKeyboardProfileName = selectedName,
            bindings => new Dictionary<JoypadButton, Key>(bindings),
            sectionName: "Keyboard"
        );

    public InputEditResult RenameKeyboardProfile(string? currentName, string? newName) =>
        RenameProfile(
            _keyboardProfiles,
            currentName: currentName,
            newName: newName,
            activeProfileName: ActiveKeyboardProfileName,
            selectedProfileName: SelectedKeyboardProfileName,
            setActiveName: activeName => ActiveKeyboardProfileName = activeName,
            setSelectedName: selectedName => SelectedKeyboardProfileName = selectedName,
            sectionName: "Keyboard",
            sectionLabel: "keyboard"
        );

    public InputEditResult DeleteKeyboardProfile(string? name) =>
        DeleteProfile(
            _keyboardProfiles,
            name: name,
            activeProfileName: ActiveKeyboardProfileName,
            selectedProfileName: SelectedKeyboardProfileName,
            setSelectedName: selectedName => SelectedKeyboardProfileName = selectedName,
            sectionName: "Keyboard",
            sectionLabel: "keyboard"
        );

    public InputEditResult SetActiveKeyboardProfile(string? name) =>
        SetActiveProfile(
            _keyboardProfiles,
            name: name,
            setActiveName: activeName => ActiveKeyboardProfileName = activeName,
            sectionName: "Keyboard"
        );

    public InputEditResult SelectGamepadProfile(string? name) =>
        SelectProfile(
            _gamepadProfiles,
            name: name,
            setSelectedName: selectedName => SelectedGamepadProfileName = selectedName,
            sectionName: "Gamepad"
        );

    public InputEditResult CreateGamepadProfile(string? name) =>
        CreateProfile(
            _gamepadProfiles,
            name: name,
            selectedName: SelectedGamepadProfileName,
            setSelectedName: selectedName => SelectedGamepadProfileName = selectedName,
            bindings => new Dictionary<JoypadButton, GamepadButton>(bindings),
            sectionName: "Gamepad"
        );

    public InputEditResult RenameGamepadProfile(string? currentName, string? newName) =>
        RenameProfile(
            _gamepadProfiles,
            currentName: currentName,
            newName: newName,
            activeProfileName: ActiveGamepadProfileName,
            selectedProfileName: SelectedGamepadProfileName,
            setActiveName: activeName => ActiveGamepadProfileName = activeName,
            setSelectedName: selectedName => SelectedGamepadProfileName = selectedName,
            sectionName: "Gamepad",
            sectionLabel: "gamepad"
        );

    public InputEditResult DeleteGamepadProfile(string? name) =>
        DeleteProfile(
            _gamepadProfiles,
            name: name,
            activeProfileName: ActiveGamepadProfileName,
            selectedProfileName: SelectedGamepadProfileName,
            setSelectedName: selectedName => SelectedGamepadProfileName = selectedName,
            sectionName: "Gamepad",
            sectionLabel: "gamepad"
        );

    public InputEditResult SetActiveGamepadProfile(string? name) =>
        SetActiveProfile(
            _gamepadProfiles,
            name: name,
            setActiveName: activeName => ActiveGamepadProfileName = activeName,
            sectionName: "Gamepad"
        );

    public Key GetKeyboardBinding(string profileName, JoypadButton button)
    {
        if (!InputConfigMetadata.KeyboardButtons.Contains(button))
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(button),
                actualValue: button,
                message: "Unknown joypad button."
            );
        }

        return GetProfile(_keyboardProfiles, profileName: profileName, sectionName: "Keyboard")[
            button
        ];
    }

    public InputEditResult SetKeyboardBinding(string? profileName, JoypadButton button, Key key)
    {
        if (
            !TryFindProfileName(
                profiles: _keyboardProfiles,
                name: profileName,
                existingName: out var existingName
            )
        )
        {
            return InputEditResult.Fail($"Keyboard profile '{profileName}' does not exist.");
        }

        if (!InputConfigMetadata.KeyboardButtons.Contains(button))
        {
            return InputEditResult.Fail("Unknown keyboard joypad button.");
        }

        if (!Enum.IsDefined(key) || key is Key.None)
        {
            return InputEditResult.Fail("Unknown keyboard key.");
        }

        if (InputConfigValidator.IsReservedKey(key))
        {
            return InputEditResult.Fail($"Keyboard key '{Enum.GetName(value: key)}' is reserved.");
        }

        _keyboardProfiles[existingName][button] = key;
        return InputEditResult.Success();
    }

    public GamepadButton GetGamepadBinding(string profileName, JoypadButton button)
    {
        if (!InputConfigMetadata.GamepadButtons.Contains(button))
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(button),
                actualValue: button,
                message: "Unknown gamepad joypad button."
            );
        }

        return GetProfile(_gamepadProfiles, profileName: profileName, sectionName: "Gamepad")[
            button
        ];
    }

    public InputEditResult SetGamepadBinding(
        string? profileName,
        JoypadButton button,
        GamepadButton control
    )
    {
        if (
            !TryFindProfileName(
                profiles: _gamepadProfiles,
                name: profileName,
                existingName: out var existingName
            )
        )
        {
            return InputEditResult.Fail($"Gamepad profile '{profileName}' does not exist.");
        }

        if (!InputConfigMetadata.GamepadButtons.Contains(button))
        {
            return InputEditResult.Fail("Unknown gamepad joypad button.");
        }

        if (
            !Enum.IsDefined(control)
            || !InputConfigMetadata.AllowedGamepadControls.Contains(control)
        )
        {
            return InputEditResult.Fail("Unknown or unsupported gamepad control.");
        }

        _gamepadProfiles[existingName][button] = control;
        return InputEditResult.Success();
    }

    public IReadOnlyList<JoypadButton> GetKeyboardBindingConflicts(string profileName) =>
        GetBindingConflicts(
            GetProfile(_keyboardProfiles, profileName: profileName, sectionName: "Keyboard"),
            InputConfigMetadata.KeyboardButtons
        );

    public IReadOnlyList<JoypadButton> GetGamepadBindingConflicts(string profileName) =>
        GetBindingConflicts(
            GetProfile(_gamepadProfiles, profileName: profileName, sectionName: "Gamepad"),
            InputConfigMetadata.GamepadButtons
        );

    public IReadOnlyList<string> Validate() => InputConfigValidator.Validate(CreateConfig());

    public InputConfig Build() => CreateConfig();

    private InputConfig CreateConfig() =>
        new()
        {
            Version = InputConfig.SupportedVersion,
            Keyboard = new KeyboardInputConfig
            {
                ActiveProfile = ActiveKeyboardProfileName,
                Profiles = new Dictionary<string, KeyboardProfileConfig>(
                    _keyboardProfiles.Select(profile => new KeyValuePair<
                        string,
                        KeyboardProfileConfig
                    >(
                        profile.Key,
                        new KeyboardProfileConfig
                        {
                            Bindings =
                            [
                                .. InputConfigMetadata.KeyboardButtons.Select(
                                    button => new KeyboardInputBindingConfig(
                                        ButtonName: button.ToString(),
                                        KeyName: profile.Value[button].ToString()
                                    )
                                ),
                            ],
                        }
                    )),
                    StringComparer.OrdinalIgnoreCase
                ),
            },
            Gamepad = new GamepadInputConfig
            {
                ActiveProfile = ActiveGamepadProfileName,
                Profiles = new Dictionary<string, GamepadProfileConfig>(
                    _gamepadProfiles.Select(profile => new KeyValuePair<
                        string,
                        GamepadProfileConfig
                    >(
                        profile.Key,
                        new GamepadProfileConfig
                        {
                            Bindings =
                            [
                                .. InputConfigMetadata.GamepadButtons.Select(
                                    button => new GamepadInputBindingConfig(
                                        ButtonName: button.ToString(),
                                        ControlName: profile.Value[button].ToString()
                                    )
                                ),
                            ],
                        }
                    )),
                    StringComparer.OrdinalIgnoreCase
                ),
            },
        };

    private static IReadOnlyList<InputProfileSummary> GetProfileSummaries<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string activeProfileName,
        string selectedProfileName
    ) =>
        [
            .. profiles.Keys.Select(name => new InputProfileSummary(
                name,
                IsActive: string.Equals(
                    a: name,
                    b: activeProfileName,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                ),
                IsSelected: string.Equals(
                    a: name,
                    b: selectedProfileName,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )),
        ];

    private static InputEditResult SelectProfile<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? name,
        Action<string> setSelectedName,
        string sectionName
    )
    {
        if (!TryFindProfileName(profiles, name: name, existingName: out var existingName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{name}' does not exist.");
        }

        setSelectedName(existingName);
        return InputEditResult.Success();
    }

    private static InputEditResult CreateProfile<TBinding>(
        Dictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? name,
        string selectedName,
        Action<string> setSelectedName,
        Func<Dictionary<JoypadButton, TBinding>, Dictionary<JoypadButton, TBinding>> clone,
        string sectionName
    )
    {
        var trimmedName = TrimProfileName(name);
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return InputEditResult.Fail($"{sectionName} profile name must not be blank.");
        }

        if (profiles.ContainsKey(trimmedName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{trimmedName}' already exists.");
        }

        profiles.Add(trimmedName, clone(profiles[selectedName]));
        setSelectedName(trimmedName);
        return InputEditResult.Success();
    }

    private static InputEditResult RenameProfile<TBinding>(
        Dictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? currentName,
        string? newName,
        string activeProfileName,
        string selectedProfileName,
        Action<string> setActiveName,
        Action<string> setSelectedName,
        string sectionName,
        string sectionLabel
    )
    {
        if (!TryFindProfileName(profiles, name: currentName, existingName: out var existingName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{currentName}' does not exist.");
        }

        if (
            string.Equals(
                a: existingName,
                b: InputConfig.DefaultProfileName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return InputEditResult.Fail($"Default {sectionLabel} profile cannot be renamed.");
        }

        var trimmedName = TrimProfileName(newName);
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return InputEditResult.Fail($"{sectionName} profile name must not be blank.");
        }

        if (
            !string.Equals(
                a: existingName,
                b: trimmedName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            ) && profiles.ContainsKey(trimmedName)
        )
        {
            return InputEditResult.Fail($"{sectionName} profile '{trimmedName}' already exists.");
        }

        var bindings = profiles[existingName];
        profiles.Remove(existingName);
        profiles.Add(trimmedName, bindings);

        if (
            string.Equals(
                a: activeProfileName,
                b: existingName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            setActiveName(trimmedName);
        }

        if (
            string.Equals(
                a: selectedProfileName,
                b: existingName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            setSelectedName(trimmedName);
        }

        return InputEditResult.Success();
    }

    private static InputEditResult DeleteProfile<TBinding>(
        Dictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? name,
        string activeProfileName,
        string selectedProfileName,
        Action<string> setSelectedName,
        string sectionName,
        string sectionLabel
    )
    {
        if (!TryFindProfileName(profiles, name: name, existingName: out var existingName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{name}' does not exist.");
        }

        if (
            string.Equals(
                a: existingName,
                b: InputConfig.DefaultProfileName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return InputEditResult.Fail($"Default {sectionLabel} profile cannot be deleted.");
        }

        if (
            string.Equals(
                a: existingName,
                b: activeProfileName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return InputEditResult.Fail($"Active {sectionLabel} profile cannot be deleted.");
        }

        profiles.Remove(existingName);
        if (
            string.Equals(
                a: selectedProfileName,
                b: existingName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            setSelectedName(activeProfileName);
        }

        return InputEditResult.Success();
    }

    private static InputEditResult SetActiveProfile<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? name,
        Action<string> setActiveName,
        string sectionName
    )
    {
        if (!TryFindProfileName(profiles, name: name, existingName: out var existingName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{name}' does not exist.");
        }

        setActiveName(existingName);
        return InputEditResult.Success();
    }

    private static IReadOnlyList<JoypadButton> GetBindingConflicts<TBinding>(
        IReadOnlyDictionary<JoypadButton, TBinding> bindings,
        IReadOnlyList<JoypadButton> buttons
    )
        where TBinding : notnull =>
        [
            .. buttons.Where(button =>
                bindings.Any(binding =>
                    binding.Key != button
                    && EqualityComparer<TBinding>.Default.Equals(
                        x: binding.Value,
                        y: bindings[button]
                    )
                )
            ),
        ];

    private static Dictionary<JoypadButton, TBinding> GetProfile<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? profileName,
        string sectionName
    )
    {
        if (!TryFindProfileName(profiles, name: profileName, existingName: out var existingName))
        {
            throw new ArgumentException(
                message: $"{sectionName} profile '{profileName}' does not exist.",
                paramName: nameof(profileName)
            );
        }

        return profiles[existingName];
    }

    private static bool TryFindProfileName<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? name,
        out string existingName
    )
    {
        var trimmedName = name?.Trim();
        var match = string.IsNullOrWhiteSpace(trimmedName)
            ? null
            : profiles.Keys.FirstOrDefault(profileName =>
                string.Equals(
                    a: profileName,
                    b: trimmedName,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            );

        existingName = match ?? string.Empty;
        return match is not null;
    }

    private static string FindProfileName<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string name,
        string sectionName
    ) =>
        TryFindProfileName(profiles, name: name, existingName: out var existingName)
            ? existingName
            : throw new InvalidOperationException(
                $"{sectionName} profile '{name}' does not exist."
            );

    private static string TrimProfileName(string? name) => name?.Trim() ?? string.Empty;

    private static Dictionary<JoypadButton, Key> ToKeyboardMap(KeyboardProfileConfig profile) =>
        new(
            profile.Bindings.Select(binding => new KeyValuePair<JoypadButton, Key>(
                Enum.Parse<JoypadButton>(binding.ButtonName, ignoreCase: true),
                Enum.Parse<Key>(binding.KeyName, ignoreCase: true)
            ))
        );

    private static Dictionary<JoypadButton, GamepadButton> ToGamepadMap(
        GamepadProfileConfig profile
    ) =>
        new(
            profile.Bindings.Select(binding => new KeyValuePair<JoypadButton, GamepadButton>(
                Enum.Parse<JoypadButton>(binding.ButtonName, ignoreCase: true),
                Enum.Parse<GamepadButton>(binding.ControlName, ignoreCase: true)
            ))
        );
}

internal sealed record InputEditResult(bool Succeeded, string? ErrorMessage)
{
    public static InputEditResult Success() => new(Succeeded: true, ErrorMessage: null);

    public static InputEditResult Fail(string errorMessage) => new(Succeeded: false, errorMessage);
}

internal sealed record InputProfileSummary(string Name, bool IsActive, bool IsSelected);
