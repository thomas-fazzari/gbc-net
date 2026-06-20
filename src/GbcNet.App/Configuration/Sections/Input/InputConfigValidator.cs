using System.Globalization;
using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Validates loaded input config before it is converted to runtime bindings.
/// </summary>
internal static class InputConfigValidator
{
    public static IReadOnlyList<string> Validate(InputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Version != InputConfig.SupportedVersion)
        {
            return
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Input config version {config.Version} is not supported."
                ),
            ];
        }

        if (!config.Profiles.TryGetValue(config.ActiveProfile, out var profile))
        {
            return [$"Input profile '{config.ActiveProfile}' does not exist."];
        }

        if (profile.Keyboard.Count == 0)
        {
            return
            [
                $"Input profile '{config.ActiveProfile}' must contain at least one keyboard binding.",
            ];
        }

        var usedKeys = new HashSet<Key>();

        foreach (var binding in profile.Keyboard)
        {
            if (!Enum.TryParse(binding.Button, ignoreCase: true, out JoypadButton _))
            {
                return [$"Unknown joypad button '{binding.Button}'."];
            }

            if (!Enum.TryParse(binding.Key, ignoreCase: false, out Key key))
            {
                return [$"Unknown keyboard key '{binding.Key}'."];
            }

            if (!usedKeys.Add(key))
            {
                return [$"Keyboard key '{binding.Key}' is bound more than once."];
            }
        }

        return [];
    }
}
