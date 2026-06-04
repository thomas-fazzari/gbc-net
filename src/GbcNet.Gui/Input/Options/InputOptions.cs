namespace GbcNet.Gui.Input.Options;

/// <summary>
/// Strongly typed input configuration loaded from defaults or a user config file.
/// </summary>
internal sealed class InputOptions
{
    /// <summary>
    /// Supported input configuration schema version.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Profile selected by the GUI.
    /// </summary>
    public string ActiveProfile { get; init; } = "default";

    /// <summary>
    /// Available input profiles keyed by profile name.
    /// </summary>
    public IReadOnlyDictionary<string, InputProfileOptions> Profiles { get; init; } =
        new Dictionary<string, InputProfileOptions>(StringComparer.Ordinal);

    public static InputOptions CreateDefault() =>
        new()
        {
            Profiles = new Dictionary<string, InputProfileOptions>(StringComparer.Ordinal)
            {
                ["default"] = InputProfileOptions.CreateDefault(),
            },
        };
}
