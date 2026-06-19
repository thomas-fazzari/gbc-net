namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Input bindings belonging to one configurable input profile.
/// </summary>
internal sealed class InputProfileOptions
{
    /// <summary>
    /// Keyboard bindings for this profile.
    /// </summary>
    public IReadOnlyList<KeyboardInputBindingOptions> Keyboard { get; init; } = [];
}
