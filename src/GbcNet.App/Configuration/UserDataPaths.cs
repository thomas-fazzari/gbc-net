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
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData,
                    Environment.SpecialFolderOption.Create
                ),
                DesktopDirectoryName,
                ConfigFileName
            );
        }

        return Path.Combine(
            GetXdgDirectoryPath("XDG_CONFIG_HOME", ".config"),
            LinuxDirectoryName,
            ConfigFileName
        );
    }

    private static string GetSaveDirectoryPath()
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.Create
                ),
                DesktopDirectoryName,
                SaveDirectoryName
            );
        }

        return Path.Combine(
            GetXdgDirectoryPath("XDG_DATA_HOME", Path.Combine(".local", "share")),
            LinuxDirectoryName,
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
