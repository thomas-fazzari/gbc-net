using System.Globalization;
using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Validates loaded input options before they are converted to runtime bindings.
/// </summary>
internal static class InputOptionsValidator
{
    public static IReadOnlyList<string> Validate(InputOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Version != InputOptions.SupportedVersion)
        {
            return
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Input config version {options.Version} is not supported."
                ),
            ];
        }

        if (!options.Profiles.TryGetValue(options.ActiveProfile, out var profile))
        {
            return [$"Input profile '{options.ActiveProfile}' does not exist."];
        }

        if (profile.Keyboard.Count == 0)
        {
            return
            [
                $"Input profile '{options.ActiveProfile}' must contain at least one keyboard binding.",
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
