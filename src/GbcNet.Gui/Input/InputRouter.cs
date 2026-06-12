using GbcNet.Core.Joypad;

namespace GbcNet.Gui.Input;

/// <summary>
/// Aggregates physical input states before updating emulated joypad buttons.
/// </summary>
internal sealed class InputRouter(
    IReadOnlyList<InputBinding> bindings,
    Action<JoypadButton, bool> setButtonState
)
{
    private readonly HashSet<PhysicalInput> _activeInputs = [];
    private readonly Dictionary<JoypadButton, int> _activeInputCountByButton = CreateButtonCounts();
    private readonly Dictionary<PhysicalInput, JoypadButton> _buttonByInput = bindings.ToDictionary(
        binding => binding.Input,
        binding => binding.Button
    );

    public bool Apply(PhysicalInput input, bool pressed)
    {
        if (!_buttonByInput.TryGetValue(input, out var button))
        {
            return false;
        }

        var stateChanged = pressed ? _activeInputs.Add(input) : _activeInputs.Remove(input);

        if (!stateChanged)
        {
            return true;
        }

        var activeInputCount = _activeInputCountByButton[button];
        var nextActiveInputCount = pressed ? activeInputCount + 1 : activeInputCount - 1;
        _activeInputCountByButton[button] = nextActiveInputCount;

        if (activeInputCount == 0 || nextActiveInputCount == 0)
        {
            setButtonState(button, nextActiveInputCount > 0);
        }

        return true;
    }

    public void Clear()
    {
        _activeInputs.Clear();

        foreach (var button in Enum.GetValues<JoypadButton>())
        {
            _activeInputCountByButton[button] = 0;
        }
    }

    private static Dictionary<JoypadButton, int> CreateButtonCounts() =>
        Enum.GetValues<JoypadButton>().ToDictionary(button => button, static _ => 0);
}
