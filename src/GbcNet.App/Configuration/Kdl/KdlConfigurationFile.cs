// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using KdlSharp;

namespace GbcNet.App.Configuration.Kdl;

/// <summary>
/// Loads the KDL configuration document.
/// </summary>
internal static class KdlConfigurationFile
{
    private const string TemplateResourceName = "GbcNet.App.Configuration.Templates.config.kdl";

    /// <summary>
    /// Loads the configuration document, creates it from defaults when it does not exist.
    /// </summary>
    public static KdlDocument LoadOrCreate(string path) => Parse(LoadTextOrCreate(path));

    public static string LoadTextOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            CreateDefaultFile(path);
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationException($"Config file could not be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Loads the embedded default configuration template.
    /// </summary>
    public static KdlDocument LoadTemplate()
    {
        try
        {
            return Parse(ReadTemplateText());
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            throw new ConfigurationException(
                $"Config template could not be read: {exception.Message}"
            );
        }
    }

    /// <summary>
    /// Atomically saves the configuration document.
    /// </summary>
    public static void SaveText(string path, string text)
    {
        try
        {
            WriteTextAtomically(path, text, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationException(
                $"Config file could not be written: {exception.Message}"
            );
        }
    }

    private static void CreateDefaultFile(string path)
    {
        try
        {
            WriteTextAtomically(path, ReadTemplateText(), overwrite: false);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            throw new ConfigurationException(
                $"Config file could not be created: {exception.Message}"
            );
        }
    }

    private static string ReadTemplateText()
    {
        var assembly = typeof(KdlConfigurationFile).Assembly;

        using var stream =
            assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException("Config template resource is missing.");

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static void WriteTextAtomically(string path, string text, bool overwrite)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var temporaryPath = Path.Combine(
            directoryPath ?? Environment.CurrentDirectory,
            $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp"
        );
        File.WriteAllText(temporaryPath, text);
        File.Move(temporaryPath, path, overwrite);
    }

    public static KdlDocument Parse(string text)
    {
        return KdlDocument.TryParse(text, out var document, out var exception)
            ? document
            : throw new ConfigurationException(
                $"Config file is not valid KDL: {exception?.Message ?? "Unknown parse error."}"
            );
    }
}
