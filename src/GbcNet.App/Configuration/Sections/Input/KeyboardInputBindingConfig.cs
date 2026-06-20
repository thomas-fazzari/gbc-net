namespace GbcNet.App.Configuration.Sections.Input;

/// <summary>
/// Keyboard mapping from a Game Boy button name to an Avalonia key name.
/// </summary>
internal sealed record KeyboardInputBindingConfig(string Button, string Key);
