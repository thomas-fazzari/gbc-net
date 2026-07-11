// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

internal sealed class InputConfigDraft
{
    private static readonly IReadOnlyList<JoypadButton> _keyboardButtons =
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

    private static readonly IReadOnlyList<JoypadButton> _gamepadButtons =
    [
        JoypadButton.A,
        JoypadButton.B,
        JoypadButton.Start,
        JoypadButton.Select,
    ];

    private static readonly HashSet<GamepadButton> _allowedGamepadControls =
    [
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
    ];

    private readonly Dictionary<string, Dictionary<JoypadButton, Key>> _keyboardProfiles;
    private readonly Dictionary<string, Dictionary<JoypadButton, GamepadButton>> _gamepadProfiles;

    public InputConfigDraft(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = InputConfigValidator.Validate(config);
        if (errors.Count != 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(config));
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
            config.Keyboard.ActiveProfile,
            "Keyboard"
        );
        SelectedKeyboardProfileName = ActiveKeyboardProfileName;
        ActiveGamepadProfileName = FindProfileName(
            _gamepadProfiles,
            config.Gamepad.ActiveProfile,
            "Gamepad"
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
            ActiveKeyboardProfileName,
            SelectedKeyboardProfileName
        );

    public IReadOnlyList<InputProfileSummary> GamepadProfiles =>
        GetProfileSummaries(_gamepadProfiles, ActiveGamepadProfileName, SelectedGamepadProfileName);

    public IReadOnlyList<JoypadButton> KeyboardBindingConflicts =>
        GetKeyboardBindingConflicts(SelectedKeyboardProfileName);

    public IReadOnlyList<JoypadButton> GamepadBindingConflicts =>
        GetGamepadBindingConflicts(SelectedGamepadProfileName);

    public InputEditResult SelectKeyboardProfile(string? name) =>
        SelectProfile(
            _keyboardProfiles,
            name,
            selectedName => SelectedKeyboardProfileName = selectedName,
            "Keyboard"
        );

    public InputEditResult CreateKeyboardProfile(string? name) =>
        CreateProfile(
            _keyboardProfiles,
            name,
            SelectedKeyboardProfileName,
            selectedName => SelectedKeyboardProfileName = selectedName,
            bindings => new Dictionary<JoypadButton, Key>(bindings),
            "Keyboard"
        );

    public InputEditResult RenameKeyboardProfile(string? currentName, string? newName) =>
        RenameProfile(
            _keyboardProfiles,
            currentName,
            newName,
            ActiveKeyboardProfileName,
            SelectedKeyboardProfileName,
            activeName => ActiveKeyboardProfileName = activeName,
            selectedName => SelectedKeyboardProfileName = selectedName,
            "Keyboard",
            "keyboard"
        );

    public InputEditResult DeleteKeyboardProfile(string? name) =>
        DeleteProfile(
            _keyboardProfiles,
            name,
            ActiveKeyboardProfileName,
            SelectedKeyboardProfileName,
            selectedName => SelectedKeyboardProfileName = selectedName,
            "Keyboard",
            "keyboard"
        );

    public InputEditResult SetActiveKeyboardProfile(string? name) =>
        SetActiveProfile(
            _keyboardProfiles,
            name,
            activeName => ActiveKeyboardProfileName = activeName,
            "Keyboard"
        );

    public InputEditResult SelectGamepadProfile(string? name) =>
        SelectProfile(
            _gamepadProfiles,
            name,
            selectedName => SelectedGamepadProfileName = selectedName,
            "Gamepad"
        );

    public InputEditResult CreateGamepadProfile(string? name) =>
        CreateProfile(
            _gamepadProfiles,
            name,
            SelectedGamepadProfileName,
            selectedName => SelectedGamepadProfileName = selectedName,
            bindings => new Dictionary<JoypadButton, GamepadButton>(bindings),
            "Gamepad"
        );

    public InputEditResult RenameGamepadProfile(string? currentName, string? newName) =>
        RenameProfile(
            _gamepadProfiles,
            currentName,
            newName,
            ActiveGamepadProfileName,
            SelectedGamepadProfileName,
            activeName => ActiveGamepadProfileName = activeName,
            selectedName => SelectedGamepadProfileName = selectedName,
            "Gamepad",
            "gamepad"
        );

    public InputEditResult DeleteGamepadProfile(string? name) =>
        DeleteProfile(
            _gamepadProfiles,
            name,
            ActiveGamepadProfileName,
            SelectedGamepadProfileName,
            selectedName => SelectedGamepadProfileName = selectedName,
            "Gamepad",
            "gamepad"
        );

    public InputEditResult SetActiveGamepadProfile(string? name) =>
        SetActiveProfile(
            _gamepadProfiles,
            name,
            activeName => ActiveGamepadProfileName = activeName,
            "Gamepad"
        );

    public Key GetKeyboardBinding(string profileName, JoypadButton button)
    {
        if (!_keyboardButtons.Contains(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown joypad button.");
        }

        return GetProfile(_keyboardProfiles, profileName, "Keyboard")[button];
    }

    public InputEditResult SetKeyboardBinding(string? profileName, JoypadButton button, Key key)
    {
        if (!TryFindProfileName(_keyboardProfiles, profileName, out var existingName))
        {
            return InputEditResult.Fail($"Keyboard profile '{profileName}' does not exist.");
        }

        if (!_keyboardButtons.Contains(button))
        {
            return InputEditResult.Fail("Unknown keyboard joypad button.");
        }

        if (!Enum.IsDefined(key) || key is Key.None)
        {
            return InputEditResult.Fail("Unknown keyboard key.");
        }

        if (InputConfigValidator.IsReservedKey(key))
        {
            return InputEditResult.Fail($"Keyboard key '{Enum.GetName(key)}' is reserved.");
        }

        _keyboardProfiles[existingName][button] = key;
        return InputEditResult.Success();
    }

    public GamepadButton GetGamepadBinding(string profileName, JoypadButton button)
    {
        if (!_gamepadButtons.Contains(button))
        {
            throw new ArgumentOutOfRangeException(
                nameof(button),
                button,
                "Unknown gamepad joypad button."
            );
        }

        return GetProfile(_gamepadProfiles, profileName, "Gamepad")[button];
    }

    public InputEditResult SetGamepadBinding(
        string? profileName,
        JoypadButton button,
        GamepadButton control
    )
    {
        if (!TryFindProfileName(_gamepadProfiles, profileName, out var existingName))
        {
            return InputEditResult.Fail($"Gamepad profile '{profileName}' does not exist.");
        }

        if (!_gamepadButtons.Contains(button))
        {
            return InputEditResult.Fail("Unknown gamepad joypad button.");
        }

        if (!Enum.IsDefined(control) || !_allowedGamepadControls.Contains(control))
        {
            return InputEditResult.Fail("Unknown or unsupported gamepad control.");
        }

        _gamepadProfiles[existingName][button] = control;
        return InputEditResult.Success();
    }

    public IReadOnlyList<JoypadButton> GetKeyboardBindingConflicts(string profileName) =>
        GetBindingConflicts(
            GetProfile(_keyboardProfiles, profileName, "Keyboard"),
            _keyboardButtons
        );

    public IReadOnlyList<JoypadButton> GetGamepadBindingConflicts(string profileName) =>
        GetBindingConflicts(GetProfile(_gamepadProfiles, profileName, "Gamepad"), _gamepadButtons);

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
                                .. _keyboardButtons.Select(button => new KeyboardInputBindingConfig(
                                    button.ToString(),
                                    profile.Value[button].ToString()
                                )),
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
                                .. _gamepadButtons.Select(button => new GamepadInputBindingConfig(
                                    button.ToString(),
                                    profile.Value[button].ToString()
                                )),
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
                string.Equals(name, activeProfileName, StringComparison.OrdinalIgnoreCase),
                string.Equals(name, selectedProfileName, StringComparison.OrdinalIgnoreCase)
            )),
        ];

    private static InputEditResult SelectProfile<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? name,
        Action<string> setSelectedName,
        string sectionName
    )
    {
        if (!TryFindProfileName(profiles, name, out var existingName))
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
        if (!TryFindProfileName(profiles, currentName, out var existingName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{currentName}' does not exist.");
        }

        if (
            string.Equals(
                existingName,
                InputConfig.DefaultProfileName,
                StringComparison.OrdinalIgnoreCase
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
            !string.Equals(existingName, trimmedName, StringComparison.OrdinalIgnoreCase)
            && profiles.ContainsKey(trimmedName)
        )
        {
            return InputEditResult.Fail($"{sectionName} profile '{trimmedName}' already exists.");
        }

        var bindings = profiles[existingName];
        profiles.Remove(existingName);
        profiles.Add(trimmedName, bindings);

        if (string.Equals(activeProfileName, existingName, StringComparison.OrdinalIgnoreCase))
        {
            setActiveName(trimmedName);
        }

        if (string.Equals(selectedProfileName, existingName, StringComparison.OrdinalIgnoreCase))
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
        if (!TryFindProfileName(profiles, name, out var existingName))
        {
            return InputEditResult.Fail($"{sectionName} profile '{name}' does not exist.");
        }

        if (
            string.Equals(
                existingName,
                InputConfig.DefaultProfileName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return InputEditResult.Fail($"Default {sectionLabel} profile cannot be deleted.");
        }

        if (string.Equals(existingName, activeProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return InputEditResult.Fail($"Active {sectionLabel} profile cannot be deleted.");
        }

        profiles.Remove(existingName);
        if (string.Equals(selectedProfileName, existingName, StringComparison.OrdinalIgnoreCase))
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
        if (!TryFindProfileName(profiles, name, out var existingName))
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
                    && EqualityComparer<TBinding>.Default.Equals(binding.Value, bindings[button])
                )
            ),
        ];

    private static Dictionary<JoypadButton, TBinding> GetProfile<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string? profileName,
        string sectionName
    )
    {
        if (!TryFindProfileName(profiles, profileName, out var existingName))
        {
            throw new ArgumentException(
                $"{sectionName} profile '{profileName}' does not exist.",
                nameof(profileName)
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
                string.Equals(profileName, trimmedName, StringComparison.OrdinalIgnoreCase)
            );

        existingName = match ?? string.Empty;
        return match is not null;
    }

    private static string FindProfileName<TBinding>(
        IReadOnlyDictionary<string, Dictionary<JoypadButton, TBinding>> profiles,
        string name,
        string sectionName
    ) =>
        TryFindProfileName(profiles, name, out var existingName)
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
    public static InputEditResult Success() => new(true, null);

    public static InputEditResult Fail(string errorMessage) => new(false, errorMessage);
}

internal sealed record InputProfileSummary(string Name, bool IsActive, bool IsSelected);
