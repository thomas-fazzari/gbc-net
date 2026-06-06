using FluentResults;
using KdlSharp;
using KdlSharp.Exceptions;

namespace GbcNet.Gui.Configuration;

/// <summary>
/// Loads the KDL configuration document.
/// </summary>
internal static class KdlConfigurationFile
{
    private const string TemplateDirectoryName = "Configuration";
    private const string TemplateSubdirectoryName = "Templates";
    internal const string FileName = "config.kdl";

    public static string UserConfigPath { get; } =
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") switch
            {
                { } xdgConfigHome when !string.IsNullOrWhiteSpace(xdgConfigHome) => xdgConfigHome,
                _ => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config"
                ),
            },
            ApplicationDirectoryNames.Linux,
            FileName
        );

    /// <summary>
    /// Loads the configuration document, creates it from defaults when it does not exist.
    /// </summary>
    public static Result<KdlDocument> LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            Result created = CreateDefaultFile(path);

            if (created.IsFailed)
            {
                return created.ToResult<KdlDocument>();
            }
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"Config file could not be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Loads the embedded default configuration template.
    /// </summary>
    public static Result<KdlDocument> LoadTemplate()
    {
        try
        {
            return Parse(File.ReadAllText(GetTemplatePath()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"Config template could not be read: {exception.Message}");
        }
    }

    private static Result CreateDefaultFile(string path)
    {
        try
        {
            string? directoryPath = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string temporaryDirectoryPath = directoryPath ?? Environment.CurrentDirectory;
            string temporaryPath = Path.Combine(
                temporaryDirectoryPath,
                $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp"
            );

            File.Copy(GetTemplatePath(), temporaryPath);
            File.Move(temporaryPath, path);
            return Result.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"Config file could not be created: {exception.Message}");
        }
    }

    private static string GetTemplatePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            TemplateDirectoryName,
            TemplateSubdirectoryName,
            FileName
        );

    private static Result<KdlDocument> Parse(string text)
    {
        if (KdlDocument.TryParse(text, out KdlDocument? document, out KdlParseException? exception))
        {
            return Result.Ok(document);
        }

        return Result.Fail(
            $"Config file is not valid KDL: {exception?.Message ?? "Unknown parse error."}"
        );
    }
}
