using FluentResults;
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
    public static Result<KdlDocument> LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = CreateDefaultFile(path);

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

    public static Result<string> LoadTextOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = CreateDefaultFile(path);

            if (created.IsFailed)
            {
                return created.ToResult<string>();
            }
        }

        try
        {
            return File.ReadAllText(path);
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
            return Parse(ReadTemplateText());
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            return Result.Fail($"Config template could not be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Atomically saves the configuration document.
    /// </summary>
    public static Result SaveText(string path, string text)
    {
        try
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
            File.Move(temporaryPath, path, overwrite: true);
            return Result.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"Config file could not be written: {exception.Message}");
        }
    }

    private static Result CreateDefaultFile(string path)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var temporaryDirectoryPath = directoryPath ?? Environment.CurrentDirectory;
            var temporaryPath = Path.Combine(
                temporaryDirectoryPath,
                $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp"
            );

            File.WriteAllText(temporaryPath, ReadTemplateText());
            File.Move(temporaryPath, path);
            return Result.Ok();
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            return Result.Fail($"Config file could not be created: {exception.Message}");
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

    private static Result<KdlDocument> Parse(string text)
    {
        if (KdlDocument.TryParse(text, out var document, out var exception))
        {
            return Result.Ok(document);
        }

        return Result.Fail(
            $"Config file is not valid KDL: {exception?.Message ?? "Unknown parse error."}"
        );
    }
}
