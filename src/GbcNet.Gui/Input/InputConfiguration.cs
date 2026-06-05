using Avalonia.Input;
using GbcNet.Core.Joypad;
using GbcNet.Gui.Input.Configuration;

namespace GbcNet.Gui.Input;

/// <summary>
/// User-editable input bindings loaded from defaults or configuration.
/// </summary>
internal sealed class InputConfiguration(IReadOnlyList<InputBinding> bindings)
{
    public IReadOnlyList<InputBinding> Bindings { get; } = bindings;

    public static InputConfiguration FromOptions(InputOptions options)
    {
        InputProfileOptions profile = options.Profiles[options.ActiveProfile];

        return new InputConfiguration([
            .. profile.Keyboard.Select(binding =>
                CreateKeyboardBinding(
                    Enum.Parse<Key>(binding.Key),
                    Enum.Parse<JoypadButton>(binding.Button, ignoreCase: true)
                )
            ),
        ]);
    }

    private static InputBinding CreateKeyboardBinding(Key key, JoypadButton button) =>
        new(new PhysicalInput(InputDeviceKind.Keyboard, key.ToString()), button);
}
