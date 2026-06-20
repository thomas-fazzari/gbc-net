using Avalonia.Input;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// User-editable input bindings loaded from defaults or configuration.
/// </summary>
internal sealed class InputMap(IReadOnlyList<InputBinding> bindings)
{
    public IReadOnlyList<InputBinding> Bindings { get; } = bindings;

    public static InputMap FromConfig(InputConfig config)
    {
        var profile = config.Profiles[config.ActiveProfile];

        return new InputMap([
            .. profile.Keyboard.Select(binding => new InputBinding(
                Enum.Parse<Key>(binding.Key),
                Enum.Parse<JoypadButton>(binding.Button, ignoreCase: true)
            )),
        ]);
    }
}
