// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.App.Emulation;
using GbcNet.App.Infrastructure.Storage;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Infrastructure.Configuration;

internal static class AppConfigurationFile
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters =
        {
            new JsonStringEnumConverter<HardwareModel>(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<EmulationSpeed>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false
            ),
            new JsonStringEnumConverter<LibraryViewMode>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false
            ),
        },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static AppConfig LoadOrCreate(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            var config = CreateDefault();
            Save(path, config, logger);
            return config;
        }

        return Load(path);
    }

    public static AppConfig Load(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), _jsonOptions)
                ?? throw new ConfigurationException("Configuration file is empty.");
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException(
                message: "Configuration file could not be parsed: " + exception.Message,
                innerException: exception
            );
        }
        catch (Exception exception) when (FileUtils.IsExpectedFileSystemException(exception))
        {
            throw new ConfigurationException(
                message: "Configuration file could not be read: " + exception.Message,
                innerException: exception
            );
        }
    }

    public static void Save(string path, AppConfig config, ILogger logger)
    {
        var temporaryPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(
                path: temporaryPath,
                contents: JsonSerializer.Serialize(config, _jsonOptions) + Environment.NewLine
            );
            File.Move(sourceFileName: temporaryPath, destFileName: path, overwrite: true);
        }
        catch (Exception exception) when (FileUtils.IsExpectedFileSystemException(exception))
        {
            FileUtils.TryDeleteRegularFile(
                temporaryPath,
                ex => AppConfigurationFileLog.TemporaryConfigurationFileCleanupFailed(logger, ex)
            );

            throw new ConfigurationException(
                message: "Configuration file could not be saved: " + exception.Message,
                innerException: exception
            );
        }
    }

    public static AppConfig CreateDefault() => new() { Input = CreateDefaultInputConfig() };

    public static InputConfig CreateDefaultInputConfig() =>
        new()
        {
            Version = InputConfig.SupportedVersion,
            Keyboard = new()
            {
                ActiveProfile = InputConfig.DefaultProfileName,
                Profiles = new Dictionary<string, KeyboardProfileConfig>(
                    StringComparer.OrdinalIgnoreCase
                )
                {
                    [InputConfig.DefaultProfileName] = new()
                    {
                        Bindings =
                        [
                            new(ButtonName: "Up", KeyName: "Up"),
                            new(ButtonName: "Down", KeyName: "Down"),
                            new(ButtonName: "Left", KeyName: "Left"),
                            new(ButtonName: "Right", KeyName: "Right"),
                            new(ButtonName: "A", KeyName: "Z"),
                            new(ButtonName: "B", KeyName: "X"),
                            new(ButtonName: "Start", KeyName: "Enter"),
                            new(ButtonName: "Select", KeyName: "Back"),
                        ],
                    },
                },
            },
            Gamepad = new()
            {
                ActiveProfile = InputConfig.DefaultProfileName,
                Profiles = new Dictionary<string, GamepadProfileConfig>(
                    StringComparer.OrdinalIgnoreCase
                )
                {
                    [InputConfig.DefaultProfileName] = new()
                    {
                        Bindings =
                        [
                            new(ButtonName: "A", ControlName: "East"),
                            new(ButtonName: "B", ControlName: "South"),
                            new(ButtonName: "Start", ControlName: "Start"),
                            new(ButtonName: "Select", ControlName: "Back"),
                        ],
                    },
                },
            },
        };
}

internal static partial class AppConfigurationFileLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Temporary configuration file cleanup failed."
    )]
    internal static partial void TemporaryConfigurationFileCleanupFailed(
        ILogger logger,
        Exception exception
    );
}
