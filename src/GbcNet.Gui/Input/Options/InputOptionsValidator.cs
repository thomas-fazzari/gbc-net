using Avalonia.Input;
using FluentResults;
using GbcNet.Core.Joypad;

namespace GbcNet.Gui.Input.Options;

/// <summary>
/// Validates loaded input options before they are converted to runtime bindings.
/// </summary>
internal static class InputOptionsValidator
{
    private const int SupportedVersion = 1;

    public static Result Validate(InputOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Version != SupportedVersion)
        {
            return Result.Fail($"Input config version {options.Version} is not supported.");
        }

        if (!options.Profiles.TryGetValue(options.ActiveProfile, out InputProfileOptions? profile))
        {
            return Result.Fail($"Input profile '{options.ActiveProfile}' does not exist.");
        }

        var usedKeys = new HashSet<Key>();

        foreach (KeyboardInputBindingOptions binding in profile.Keyboard)
        {
            if (!Enum.TryParse(binding.Button, ignoreCase: true, out JoypadButton _))
            {
                return Result.Fail($"Unknown joypad button '{binding.Button}'.");
            }

            if (!Enum.TryParse(binding.Key, ignoreCase: false, out Key key))
            {
                return Result.Fail($"Unknown keyboard key '{binding.Key}'.");
            }

            if (!usedKeys.Add(key))
            {
                return Result.Fail($"Keyboard key '{binding.Key}' is bound more than once.");
            }
        }

        return Result.Ok();
    }
}
