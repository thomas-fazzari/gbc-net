// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

internal sealed class InputConfigDraft
{
    private readonly ProfileSection<Key> _keyboard;
    private readonly ProfileSection<GamepadButton> _gamepad;

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

        _keyboard = new ProfileSection<Key>(
            config.Keyboard.Profiles.Select(static p => new KeyValuePair<
                string,
                IReadOnlyList<IInputBindingConfig>
            >(p.Key, p.Value.Bindings)),
            config.Keyboard.ActiveProfile,
            static p => new Dictionary<JoypadButton, Key>(p),
            "Keyboard",
            "keyboard"
        );
        _gamepad = new ProfileSection<GamepadButton>(
            config.Gamepad.Profiles.Select(static p => new KeyValuePair<
                string,
                IReadOnlyList<IInputBindingConfig>
            >(p.Key, p.Value.Bindings)),
            config.Gamepad.ActiveProfile,
            static p => new Dictionary<JoypadButton, GamepadButton>(p),
            "Gamepad",
            "gamepad"
        );
    }

    public string ActiveKeyboardProfileName => _keyboard.ActiveName;
    public string SelectedKeyboardProfileName => _keyboard.SelectedName;
    public string ActiveGamepadProfileName => _gamepad.ActiveName;
    public string SelectedGamepadProfileName => _gamepad.SelectedName;

    public IReadOnlyList<InputProfileSummary> KeyboardProfiles => _keyboard.Summaries;
    public IReadOnlyList<InputProfileSummary> GamepadProfiles => _gamepad.Summaries;

    public IReadOnlyList<JoypadButton> KeyboardBindingConflicts =>
        _keyboard.GetBindingConflicts(SelectedKeyboardProfileName);
    public IReadOnlyList<JoypadButton> GamepadBindingConflicts =>
        _gamepad.GetBindingConflicts(SelectedGamepadProfileName);

    public InputEditResult SelectKeyboardProfile(string? name) => _keyboard.Select(name);

    public InputEditResult CreateKeyboardProfile(string? name) => _keyboard.Create(name);

    public InputEditResult RenameKeyboardProfile(string? currentName, string? newName) =>
        _keyboard.Rename(currentName, newName);

    public InputEditResult DeleteKeyboardProfile(string? name) => _keyboard.Delete(name);

    public InputEditResult SetActiveKeyboardProfile(string? name) => _keyboard.SetActive(name);

    public InputEditResult SelectGamepadProfile(string? name) => _gamepad.Select(name);

    public InputEditResult CreateGamepadProfile(string? name) => _gamepad.Create(name);

    public InputEditResult RenameGamepadProfile(string? currentName, string? newName) =>
        _gamepad.Rename(currentName, newName);

    public InputEditResult DeleteGamepadProfile(string? name) => _gamepad.Delete(name);

    public InputEditResult SetActiveGamepadProfile(string? name) => _gamepad.SetActive(name);

    public Key GetKeyboardBinding(string profileName, JoypadButton button) =>
        _keyboard.GetBinding(profileName, button);

    public InputEditResult SetKeyboardBinding(string? profileName, JoypadButton button, Key key) =>
        _keyboard.SetBinding(profileName, button, key, ValidateKey);

    public GamepadButton GetGamepadBinding(string profileName, JoypadButton button) =>
        _gamepad.GetBinding(profileName, button);

    public InputEditResult SetGamepadBinding(
        string? profileName,
        JoypadButton button,
        GamepadButton control
    ) => _gamepad.SetBinding(profileName, button, control, ValidateControl);

    public IReadOnlyList<string> Validate() => InputConfigValidator.Validate(CreateConfig());

    public InputConfig Build() => CreateConfig();

    private static InputEditResult ValidateKey(Key key)
    {
        if (!Enum.IsDefined(key) || key is Key.None)
        {
            return InputEditResult.Fail("Unknown keyboard key.");
        }

        return InputConfigValidator.IsReservedKey(key)
            ? InputEditResult.Fail($"Keyboard key '{Enum.GetName(value: key)}' is reserved.")
            : InputEditResult.Success();
    }

    private static InputEditResult ValidateControl(GamepadButton control) =>
        !Enum.IsDefined(control) || !InputConfigMetadata.AllowedGamepadControls.Contains(control)
            ? InputEditResult.Fail("Unknown or unsupported gamepad control.")
            : InputEditResult.Success();

    private InputConfig CreateConfig() =>
        new()
        {
            Version = InputConfig.SupportedVersion,
            Keyboard = new KeyboardInputConfig
            {
                ActiveProfile = ActiveKeyboardProfileName,
                Profiles = _keyboard.BuildProfiles(static b => new KeyboardProfileConfig
                {
                    Bindings =
                    [
                        .. b.Select(static kv => new KeyboardInputBindingConfig(
                            kv.Key.ToString(),
                            kv.Value.ToString()
                        )),
                    ],
                }),
            },
            Gamepad = new GamepadInputConfig
            {
                ActiveProfile = ActiveGamepadProfileName,
                Profiles = _gamepad.BuildProfiles(static b => new GamepadProfileConfig
                {
                    Bindings =
                    [
                        .. b.Select(static kv => new GamepadInputBindingConfig(
                            kv.Key.ToString(),
                            kv.Value.ToString()
                        )),
                    ],
                }),
            },
        };

    private sealed class ProfileSection<TBinding>
        where TBinding : struct, Enum
    {
        private readonly Dictionary<string, Dictionary<JoypadButton, TBinding>> _profiles;
        private readonly Func<
            IReadOnlyDictionary<JoypadButton, TBinding>,
            Dictionary<JoypadButton, TBinding>
        > _clone;
        private readonly string _sectionName;
        private readonly string _sectionLabel;

        public string ActiveName { get; private set; }
        public string SelectedName { get; private set; }

        public IReadOnlyList<InputProfileSummary> Summaries =>
            [
                .. _profiles.Keys.Select(name => new InputProfileSummary(
                    name,
                    IsActive: Eq(name, ActiveName),
                    IsSelected: Eq(name, SelectedName)
                )),
            ];

        public ProfileSection(
            IEnumerable<KeyValuePair<string, IReadOnlyList<IInputBindingConfig>>> rawProfiles,
            string activeProfileName,
            Func<
                IReadOnlyDictionary<JoypadButton, TBinding>,
                Dictionary<JoypadButton, TBinding>
            > clone,
            string sectionName,
            string sectionLabel
        )
        {
            _profiles = new Dictionary<string, Dictionary<JoypadButton, TBinding>>(
                StringComparer.OrdinalIgnoreCase
            );
            _clone = clone;
            _sectionName = sectionName;
            _sectionLabel = sectionLabel;

            foreach (var (name, bindings) in rawProfiles)
            {
                _profiles.Add(name, ParseBindings(bindings));
            }

            ActiveName = FindProfileName(activeProfileName);
            SelectedName = ActiveName;
        }

        public InputEditResult Select(string? name)
        {
            if (!TryFindProfileName(name, out var existingName))
            {
                return FailNotFound(name);
            }

            SelectedName = existingName;
            return InputEditResult.Success();
        }

        public InputEditResult Create(string? name)
        {
            var trimmedName = TrimName(name);
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return InputEditResult.Fail($"{_sectionName} profile name must not be blank.");
            }

            if (_profiles.ContainsKey(trimmedName))
            {
                return InputEditResult.Fail(
                    $"{_sectionName} profile '{trimmedName}' already exists."
                );
            }

            _profiles.Add(trimmedName, _clone(_profiles[SelectedName]));
            SelectedName = trimmedName;
            return InputEditResult.Success();
        }

        public InputEditResult Rename(string? currentName, string? newName)
        {
            if (!TryFindProfileName(currentName, out var existingName))
            {
                return FailNotFound(currentName);
            }

            if (Eq(existingName, InputConfig.DefaultProfileName))
            {
                return InputEditResult.Fail($"Default {_sectionLabel} profile cannot be renamed.");
            }

            var trimmedName = TrimName(newName);
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return InputEditResult.Fail($"{_sectionName} profile name must not be blank.");
            }

            if (!Eq(existingName, trimmedName) && _profiles.ContainsKey(trimmedName))
            {
                return InputEditResult.Fail(
                    $"{_sectionName} profile '{trimmedName}' already exists."
                );
            }

            var bindings = _profiles[existingName];
            _profiles.Remove(existingName);
            _profiles.Add(trimmedName, bindings);

            if (Eq(ActiveName, existingName))
            {
                ActiveName = trimmedName;
            }

            if (Eq(SelectedName, existingName))
            {
                SelectedName = trimmedName;
            }

            return InputEditResult.Success();
        }

        public InputEditResult Delete(string? name)
        {
            if (!TryFindProfileName(name, out var existingName))
            {
                return FailNotFound(name);
            }

            if (Eq(existingName, InputConfig.DefaultProfileName))
            {
                return InputEditResult.Fail($"Default {_sectionLabel} profile cannot be deleted.");
            }

            if (Eq(existingName, ActiveName))
            {
                return InputEditResult.Fail($"Active {_sectionLabel} profile cannot be deleted.");
            }

            _profiles.Remove(existingName);
            if (Eq(SelectedName, existingName))
            {
                SelectedName = ActiveName;
            }

            return InputEditResult.Success();
        }

        public InputEditResult SetActive(string? name)
        {
            if (!TryFindProfileName(name, out var existingName))
            {
                return FailNotFound(name);
            }

            ActiveName = existingName;
            return InputEditResult.Success();
        }

        public TBinding GetBinding(string profileName, JoypadButton button)
        {
            if (!InputConfigMetadata.ButtonsFor<TBinding>().Contains(button))
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(button),
                    actualValue: button,
                    message: $"Unknown {_sectionLabel} joypad button."
                );
            }

            return GetProfile(profileName)[button];
        }

        public InputEditResult SetBinding(
            string? profileName,
            JoypadButton button,
            TBinding binding,
            Func<TBinding, InputEditResult> validateBinding
        )
        {
            if (!TryFindProfileName(profileName, out var existingName))
            {
                return FailNotFound(profileName);
            }

            if (!InputConfigMetadata.ButtonsFor<TBinding>().Contains(button))
            {
                return InputEditResult.Fail($"Unknown {_sectionLabel} joypad button.");
            }

            var result = validateBinding(binding);
            if (!result.Succeeded)
            {
                return result;
            }

            _profiles[existingName][button] = binding;
            return InputEditResult.Success();
        }

        public IReadOnlyList<JoypadButton> GetBindingConflicts(string profileName) =>
            GetBindingConflicts(
                GetProfile(profileName),
                InputConfigMetadata.ButtonsFor<TBinding>()
            );

        public Dictionary<string, TProfile> BuildProfiles<TProfile>(
            Func<IReadOnlyDictionary<JoypadButton, TBinding>, TProfile> buildProfile
        ) =>
            new(
                _profiles.Select(profile => new KeyValuePair<string, TProfile>(
                    profile.Key,
                    buildProfile(profile.Value)
                )),
                StringComparer.OrdinalIgnoreCase
            );

        private InputEditResult FailNotFound(string? name) =>
            InputEditResult.Fail($"{_sectionName} profile '{name}' does not exist.");

        private static IReadOnlyList<JoypadButton> GetBindingConflicts(
            Dictionary<JoypadButton, TBinding> bindings,
            IReadOnlyList<JoypadButton> buttons
        ) =>
            [
                .. buttons.Where(button =>
                    bindings.Any(b =>
                        b.Key != button
                        && EqualityComparer<TBinding>.Default.Equals(b.Value, bindings[button])
                    )
                ),
            ];

        private Dictionary<JoypadButton, TBinding> GetProfile(string? profileName)
        {
            if (!TryFindProfileName(profileName, out var existingName))
            {
                throw new ArgumentException(
                    message: $"{_sectionName} profile '{profileName}' does not exist.",
                    paramName: nameof(profileName)
                );
            }

            return _profiles[existingName];
        }

        private bool TryFindProfileName(string? name, out string existingName)
        {
            var trimmedName = name?.Trim();
            var match = string.IsNullOrWhiteSpace(trimmedName)
                ? null
                : _profiles.Keys.FirstOrDefault(p => Eq(p, trimmedName));

            existingName = match ?? string.Empty;
            return match is not null;
        }

        private string FindProfileName(string name) =>
            TryFindProfileName(name, out var existingName)
                ? existingName
                : throw new InvalidOperationException(
                    $"{_sectionName} profile '{name}' does not exist."
                );

        private static string TrimName(string? name) => name?.Trim() ?? string.Empty;

        private static bool Eq(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static Dictionary<JoypadButton, TBinding> ParseBindings(
            IReadOnlyList<IInputBindingConfig> bindings
        ) =>
            new(
                bindings.Select(b => new KeyValuePair<JoypadButton, TBinding>(
                    Enum.Parse<JoypadButton>(b.ButtonName, ignoreCase: true),
                    Enum.Parse<TBinding>(b.TargetName, ignoreCase: true)
                ))
            );
    }
}

internal sealed record InputEditResult(bool Succeeded, string? ErrorMessage)
{
    public static InputEditResult Success() => new(Succeeded: true, ErrorMessage: null);

    public static InputEditResult Fail(string errorMessage) => new(Succeeded: false, errorMessage);
}

internal sealed record InputProfileSummary(string Name, bool IsActive, bool IsSelected);
