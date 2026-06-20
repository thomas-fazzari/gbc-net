namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Input bindings belonging to one configurable input profile.
/// </summary>
internal sealed class InputProfileConfig
{
    /// <summary>
    /// Keyboard bindings for this profile.
    /// </summary>
    public IReadOnlyList<KeyboardInputBindingConfig> Keyboard { get; init; } = [];
}
