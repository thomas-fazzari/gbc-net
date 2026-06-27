namespace GbcNet.App.Configuration;

/// <summary>
/// Centralizes OS-specific per-user GUI data paths.
/// </summary>
internal static class UserDataPaths
{
    internal const string ConfigFileName = "config.kdl";

    private const string LinuxDirectoryName = "gbc-net";
    private const string DesktopDirectoryName = "GbcNet";
    private const string SaveDirectoryName = "saves";
    private const string LibraryDatabaseFileName = "library.sqlite";

    /// <summary>
    /// Per-user configuration file path for the current OS.
    /// </summary>
    public static string ConfigFilePath { get; } = GetConfigFilePath();

    /// <summary>
    /// Per-user battery save directory path for the current OS.
    /// </summary>
    public static string SaveDirectoryPath { get; } = GetSaveDirectoryPath();

    /// <summary>
    /// ROM library SQLite database path for the current OS.
    /// </summary>
    public static string LibraryDatabasePath { get; } = GetLibraryDatabasePath();

    private static string GetConfigFilePath() =>
        Path.Combine(GetConfigDirectoryPath(), ConfigFileName);

    private static string GetSaveDirectoryPath() =>
        Path.Combine(GetDataDirectoryPath(), SaveDirectoryName);

    private static string GetLibraryDatabasePath() =>
        Path.Combine(GetDataDirectoryPath(), LibraryDatabaseFileName);

    private static string GetConfigDirectoryPath() =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? Path.Combine(
                GetKnownFolder(Environment.SpecialFolder.ApplicationData),
                DesktopDirectoryName
            )
            : Path.Combine(GetXdgDirectoryPath("XDG_CONFIG_HOME", ".config"), LinuxDirectoryName);

    private static string GetDataDirectoryPath() =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? Path.Combine(
                GetKnownFolder(Environment.SpecialFolder.LocalApplicationData),
                DesktopDirectoryName
            )
            : Path.Combine(
                GetXdgDirectoryPath("XDG_DATA_HOME", Path.Combine(".local", "share")),
                LinuxDirectoryName
            );

    private static string GetKnownFolder(Environment.SpecialFolder folder) =>
        Environment.GetFolderPath(folder, Environment.SpecialFolderOption.Create);

    private static string GetXdgDirectoryPath(
        string environmentVariableName,
        string fallbackDirectoryName
    )
    {
        var directoryPath = Environment.GetEnvironmentVariable(environmentVariableName);

        return string.IsNullOrWhiteSpace(directoryPath)
            ? Path.Combine(GetUserProfilePath(), fallbackDirectoryName)
            : directoryPath;
    }

    private static string GetUserProfilePath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
