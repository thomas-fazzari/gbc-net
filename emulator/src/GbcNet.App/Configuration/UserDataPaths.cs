// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Configuration;

/// <summary>
/// Centralizes OS-specific per-user GUI data paths.
/// </summary>
internal static class UserDataPaths
{
    internal const string ConfigFileName = "config.json";

    private const string LinuxDirectoryName = "gbc-net";
    private const string DesktopDirectoryName = "GbcNet";
    private const string SaveDirectoryName = "saves";
    private const string SaveStateDirectoryName = "states";
    private const string CoverDirectoryName = "covers";
    private const string LibraryDatabaseFileName = "gbcnet.sqlite";
    private const string LogDirectoryName = "logs";
    private const string LogFileName = "gbcnet-.log";

    /// <summary>
    /// Per-user configuration file path for the current OS.
    /// </summary>
    public static string ConfigFilePath { get; } = GetConfigFilePath();

    /// <summary>
    /// Per-user battery save directory path for the current OS.
    /// </summary>
    public static string SaveDirectoryPath { get; } = GetSaveDirectoryPath();

    /// <summary>
    /// Per-user manual save-state directory path for the current OS.
    /// </summary>
    public static string SaveStateDirectoryPath { get; } = GetSaveStateDirectoryPath();

    /// <summary>
    /// ROM library SQLite database path for the current OS.
    /// </summary>
    public static string LibraryDatabasePath { get; } = GetLibraryDatabasePath();

    /// <summary>
    /// Per-user managed ROM cover image directory path for the current OS.
    /// </summary>
    public static string CoverDirectoryPath { get; } = GetCoverDirectoryPath();

    /// <summary>
    /// Rolling application log file path for the current OS.
    /// </summary>
    public static string LogFilePath { get; } = GetLogFilePath();

    private static string GetConfigFilePath() =>
        Path.Combine(GetConfigDirectoryPath(), ConfigFileName);

    private static string GetSaveDirectoryPath() =>
        Path.Combine(GetDataDirectoryPath(), SaveDirectoryName);

    private static string GetSaveStateDirectoryPath() =>
        Path.Combine(GetDataDirectoryPath(), SaveStateDirectoryName);

    private static string GetCoverDirectoryPath() =>
        Path.Combine(GetDataDirectoryPath(), CoverDirectoryName);

    private static string GetLibraryDatabasePath() =>
        Path.Combine(GetDataDirectoryPath(), LibraryDatabaseFileName);

    private static string GetLogFilePath() =>
        Path.Combine(GetDataDirectoryPath(), LogDirectoryName, path3: LogFileName);

    private static string GetConfigDirectoryPath() =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? Path.Combine(
                GetKnownFolder(Environment.SpecialFolder.ApplicationData),
                DesktopDirectoryName
            )
            : Path.Combine(
                GetXdgDirectoryPath(
                    environmentVariableName: "XDG_CONFIG_HOME",
                    fallbackDirectoryName: ".config"
                ),
                LinuxDirectoryName
            );

    private static string GetDataDirectoryPath() =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? Path.Combine(
                GetKnownFolder(Environment.SpecialFolder.LocalApplicationData),
                DesktopDirectoryName
            )
            : Path.Combine(
                GetXdgDirectoryPath(
                    environmentVariableName: "XDG_DATA_HOME",
                    fallbackDirectoryName: Path.Combine(".local", "share")
                ),
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
