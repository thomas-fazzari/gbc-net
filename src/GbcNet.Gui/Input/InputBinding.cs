using GbcNet.Core.Joypad;

namespace GbcNet.Gui.Input;

/// <summary>
/// Identifies one physical control before it is mapped to a Game Boy button.
/// </summary>
internal readonly record struct PhysicalInput(InputDeviceKind DeviceKind, string Code);

/// <summary>
/// Maps one physical input to one Game Boy joypad button.
/// </summary>
internal readonly record struct InputBinding(PhysicalInput Input, JoypadButton Button);
