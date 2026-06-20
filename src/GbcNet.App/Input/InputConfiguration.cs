using Avalonia.Input;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// User-editable input bindings loaded from defaults or configuration.
/// </summary>
internal sealed class InputConfiguration(IReadOnlyList<InputBinding> bindings)
{
    public IReadOnlyList<InputBinding> Bindings { get; } = bindings;

    public static InputConfiguration FromOptions(InputOptions options)
    {
        var profile = options.Profiles[options.ActiveProfile];

        return new InputConfiguration([
            .. profile.Keyboard.Select(binding => new InputBinding(
                Enum.Parse<Key>(binding.Key),
                Enum.Parse<JoypadButton>(binding.Button, ignoreCase: true)
            )),
        ]);
    }
}
