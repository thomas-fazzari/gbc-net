using Avalonia.Input;

namespace GbcNet.App.Input;

/// <summary>
/// Maps Avalonia keyboard events to configured physical inputs.
/// </summary>
internal sealed class KeyboardInputMapper(IEnumerable<InputBinding> bindings)
{
    private readonly Dictionary<Key, PhysicalInput> _inputByKey = bindings
        .Where(binding => binding.Input.DeviceKind is InputDeviceKind.Keyboard)
        .ToDictionary(binding => Enum.Parse<Key>(binding.Input.Code), binding => binding.Input);

    public bool TryMap(Key key, out PhysicalInput input) => _inputByKey.TryGetValue(key, out input);
}
