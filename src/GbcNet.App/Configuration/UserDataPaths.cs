namespace GbcNet.App.Configuration;

/// <summary>
/// Centralizes OS-specific per-user GUI data paths.
/// </summary>
internal static class UserDataPaths
{
    private const string SaveDirectoryName = "saves";

    /// <summary>
    /// Per-user configuration file path for the current OS.
    /// </summary>
    public static string ConfigFilePath { get; } = GetConfigFilePath();

    /// <summary>
    /// Per-user battery save directory path for the current OS.
    /// </summary>
    public static string SaveDirectoryPath { get; } = GetSaveDirectoryPath();

    private static string GetConfigFilePath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                GetUserProfilePath(),
                "Library",
                "Application Support",
                ApplicationDirectoryNames.Desktop,
                ApplicationDirectoryNames.ConfigFile
            );
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ApplicationDirectoryNames.Desktop,
                ApplicationDirectoryNames.ConfigFile
            );
        }

        return Path.Combine(
            GetXdgDirectoryPath("XDG_CONFIG_HOME", ".config"),
            ApplicationDirectoryNames.Linux,
            ApplicationDirectoryNames.ConfigFile
        );
    }

    private static string GetSaveDirectoryPath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                GetUserProfilePath(),
                "Library",
                "Application Support",
                ApplicationDirectoryNames.Desktop,
                SaveDirectoryName
            );
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationDirectoryNames.Desktop,
                SaveDirectoryName
            );
        }

        return Path.Combine(
            GetXdgDirectoryPath("XDG_DATA_HOME", Path.Combine(".local", "share")),
            ApplicationDirectoryNames.Linux,
            SaveDirectoryName
        );
    }

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
