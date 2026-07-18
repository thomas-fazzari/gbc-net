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

    private static string GetConfigFilePath() =>
        Path.Combine(path1: GetConfigDirectoryPath(), path2: ConfigFileName);

    private static string GetSaveDirectoryPath() =>
        Path.Combine(path1: GetDataDirectoryPath(), path2: SaveDirectoryName);

    private static string GetSaveStateDirectoryPath() =>
        Path.Combine(path1: GetDataDirectoryPath(), path2: SaveStateDirectoryName);

    private static string GetCoverDirectoryPath() =>
        Path.Combine(path1: GetDataDirectoryPath(), path2: CoverDirectoryName);

    private static string GetLibraryDatabasePath() =>
        Path.Combine(path1: GetDataDirectoryPath(), path2: LibraryDatabaseFileName);

    private static string GetConfigDirectoryPath() =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? Path.Combine(
                path1: GetKnownFolder(Environment.SpecialFolder.ApplicationData),
                path2: DesktopDirectoryName
            )
            : Path.Combine(
                path1: GetXdgDirectoryPath(
                    environmentVariableName: "XDG_CONFIG_HOME",
                    fallbackDirectoryName: ".config"
                ),
                path2: LinuxDirectoryName
            );

    private static string GetDataDirectoryPath() =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
            ? Path.Combine(
                path1: GetKnownFolder(Environment.SpecialFolder.LocalApplicationData),
                path2: DesktopDirectoryName
            )
            : Path.Combine(
                path1: GetXdgDirectoryPath(
                    environmentVariableName: "XDG_DATA_HOME",
                    fallbackDirectoryName: Path.Combine(path1: ".local", path2: "share")
                ),
                path2: LinuxDirectoryName
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
            ? Path.Combine(path1: GetUserProfilePath(), path2: fallbackDirectoryName)
            : directoryPath;
    }

    private static string GetUserProfilePath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
