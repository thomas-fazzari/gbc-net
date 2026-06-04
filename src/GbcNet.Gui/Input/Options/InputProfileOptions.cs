namespace GbcNet.Gui.Input.Options;

/// <summary>
/// Input bindings belonging to one configurable input profile.
/// </summary>
internal sealed class InputProfileOptions
{
    /// <summary>
    /// Keyboard bindings for this profile.
    /// </summary>
    public IReadOnlyList<KeyboardInputBindingOptions> Keyboard { get; init; } = [];

    public static InputProfileOptions CreateDefault() =>
        new()
        {
            Keyboard =
            [
                new("up", "Up"),
                new("down", "Down"),
                new("left", "Left"),
                new("right", "Right"),
                new("a", "Z"),
                new("b", "X"),
                new("start", "Enter"),
                new("select", "Back"),
            ],
        };
}
