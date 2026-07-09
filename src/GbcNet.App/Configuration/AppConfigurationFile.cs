// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Emulation;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Configuration;

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
        },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static AppConfig LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var config = CreateDefault();
            Save(path, config);
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
                "Configuration file could not be parsed: " + exception.Message,
                exception
            );
        }
        catch (Exception exception) when (IsExpectedFileException(exception))
        {
            throw new ConfigurationException(
                "Configuration file could not be read: " + exception.Message,
                exception
            );
        }
    }

    public static void Save(string path, AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(config, _jsonOptions) + Environment.NewLine
            );
        }
        catch (Exception exception) when (IsExpectedFileException(exception))
        {
            throw new ConfigurationException(
                "Configuration file could not be saved: " + exception.Message,
                exception
            );
        }
    }

    public static AppConfig CreateDefault() => new() { Input = CreateDefaultInputConfig() };

    public static InputConfig CreateDefaultInputConfig() =>
        new()
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new()
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
                },
            },
        };

    private static bool IsExpectedFileException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
}
