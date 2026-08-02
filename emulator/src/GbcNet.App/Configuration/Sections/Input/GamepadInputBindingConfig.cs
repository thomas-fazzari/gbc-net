// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Serialization;

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Gamepad mapping from a Game Boy button name to a physical gamepad control name.
/// </summary>
internal sealed record GamepadInputBindingConfig(
    [property: JsonPropertyName("button")] string ButtonName,
    [property: JsonPropertyName("control")] string ControlName
);
