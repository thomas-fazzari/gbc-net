// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Infrastructure.Storage;

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
        GetApplicationDirectoryPath(Environment.SpecialFolder.ApplicationData);

    private static string GetDataDirectoryPath() =>
        GetApplicationDirectoryPath(Environment.SpecialFolder.LocalApplicationData);

    private static string GetApplicationDirectoryPath(Environment.SpecialFolder folder) =>
        Path.GetFullPath(
            Path.Combine(
                Environment.GetFolderPath(folder, Environment.SpecialFolderOption.Create),
                OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()
                    ? DesktopDirectoryName
                    : LinuxDirectoryName
            )
        );
}
