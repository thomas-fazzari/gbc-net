namespace GbcNet.Gui.Input.Configuration;

/// <summary>
/// Strongly typed input configuration loaded from defaults or a user config file.
/// </summary>
internal sealed class InputOptions
{
    /// <summary>
    /// Supported input configuration schema version.
    /// </summary>
    public const int SupportedVersion = 1;

    public int Version { get; set; } = SupportedVersion;

    /// <summary>
    /// Profile selected by the GUI.
    /// </summary>
    public string ActiveProfile { get; set; } = "default";

    /// <summary>
    /// Available input profiles keyed by profile name.
    /// </summary>
    public IReadOnlyDictionary<string, InputProfileOptions> Profiles { get; set; } =
        new Dictionary<string, InputProfileOptions>(StringComparer.Ordinal);
}
