// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Input;

/// <summary>
/// A currently connected SDL gamepad, identified by its transient SDL joystick ID.
/// </summary>
internal sealed record GamepadDeviceSnapshot(uint DeviceId, string Name, string DisplayLabel);

/// <summary>
/// Identifies the portable control pressed on the Settings-selected gamepad.
/// </summary>
internal sealed class GamepadButtonPressedEventArgs(GamepadButton button) : EventArgs
{
    public GamepadButton Button { get; } = button;
}
