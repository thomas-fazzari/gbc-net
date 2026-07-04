// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Keyboard mapping from a Game Boy button name to an Avalonia key name.
/// </summary>
internal sealed record KeyboardInputBindingConfig(string ButtonName, string KeyName);
