// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

internal readonly record struct GamepadBinding(GamepadButton Control, JoypadButton Button);
