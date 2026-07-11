// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Keyboard input profiles and the profile activated on startup.
/// </summary>
internal sealed class KeyboardInputConfig
{
    public string ActiveProfile { get; set; } = null!;

    public IReadOnlyDictionary<string, KeyboardProfileConfig> Profiles { get; set; } =
        new Dictionary<string, KeyboardProfileConfig>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Keyboard bindings belonging to one configurable input profile.
/// </summary>
internal sealed class KeyboardProfileConfig
{
    public IReadOnlyList<KeyboardInputBindingConfig> Bindings { get; init; } = [];
}
