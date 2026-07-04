// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.Core.Joypad;

namespace GbcNet.App.Input;

/// <summary>
/// Maps one keyboard key to one Game Boy joypad button.
/// </summary>
internal readonly record struct InputBinding(Key Key, JoypadButton Button);
