namespace GbcNet.Gui.Input.Configuration;

/// <summary>
/// Keyboard mapping from a Game Boy button name to an Avalonia key name.
/// </summary>
internal sealed record KeyboardInputBindingOptions(string Button, string Key);
