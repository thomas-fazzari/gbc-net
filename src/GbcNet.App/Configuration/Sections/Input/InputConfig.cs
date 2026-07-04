// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Strongly typed input configuration loaded from defaults or a user config file.
/// </summary>
internal sealed class InputConfig
{
    /// <summary>
    /// Supported input configuration schema version.
    /// </summary>
    public const int SupportedVersion = 1;

    public int Version { get; set; } = SupportedVersion;

    /// <summary>
    /// Profile activated on startup.
    /// </summary>
    public string ActiveProfile { get; set; } = InputConfigSchema.DefaultProfileName;

    /// <summary>
    /// Available input profiles keyed by profile name.
    /// </summary>
    public IReadOnlyDictionary<string, InputProfileConfig> Profiles { get; set; } =
        new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal);
}
