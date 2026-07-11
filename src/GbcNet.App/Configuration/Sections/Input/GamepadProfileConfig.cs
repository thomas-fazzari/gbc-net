// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Gamepad input profiles and the profile activated on startup.
/// </summary>
internal sealed class GamepadInputConfig
{
    public string ActiveProfile { get; set; } = null!;

    public IReadOnlyDictionary<string, GamepadProfileConfig> Profiles { get; set; } =
        new Dictionary<string, GamepadProfileConfig>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Gamepad bindings belonging to one configurable input profile.
/// </summary>
internal sealed class GamepadProfileConfig
{
    public IReadOnlyList<GamepadInputBindingConfig> Bindings { get; init; } = [];
}
