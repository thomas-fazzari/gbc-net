// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.Core.Cartridges;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Saves;

/// <summary>
/// Persists cartridge battery-backed save data under the configured save directory.
/// </summary>
internal sealed class CartridgeBatterySaveFileService(
    string saveDirectoryPath,
    ILogger<CartridgeBatterySaveFileService> logger
)
{
    private const string SaveFileExtension = ".sav";

    public string? Load(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        if (!cartridge.HasBatteryBackedSave)
        {
            return null;
        }

        var path = GetBatterySavePath(cartridge, rom);
        if (!File.Exists(path))
        {
            return path;
        }

        try
        {
            var saveLength = new FileInfo(path).Length;
            if (saveLength != cartridge.BatterySaveSize)
            {
                throw new InvalidOperationException(
                    string.Create(
                        provider: CultureInfo.InvariantCulture,
                        handler: $"Save file is {saveLength} bytes, but cartridge expects {cartridge.BatterySaveSize} bytes."
                    )
                );
            }

            if (!cartridge.TryImportBatterySave(File.ReadAllBytes(path), out var errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                message: "Save file could not be read: " + exception.Message,
                innerException: exception
            );
        }

        return path;
    }

    public async Task SaveAsync(string savePath, ReadOnlyMemory<byte> save)
    {
        var temporaryPath = $"{savePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(saveDirectoryPath);

            await File.WriteAllBytesAsync(temporaryPath, save, CancellationToken.None);
            File.Move(sourceFileName: temporaryPath, destFileName: savePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                message: "Save file could not be written: " + exception.Message,
                innerException: exception
            );
        }
        finally
        {
            FileUtils.TryDeleteRegularFile(
                temporaryPath,
                ex => CartridgeBatterySaveFileServiceLog.TemporarySaveFileCleanupFailed(logger, ex)
            );
        }
    }

    internal string GetBatterySavePath(Cartridge cartridge, ReadOnlySpan<byte> rom) =>
        Path.Combine(
            path1: saveDirectoryPath,
            path2: RomStorageIdentity.CreateFileStem(cartridge.Header.Title, rom)
                + SaveFileExtension
        );
}

internal static partial class CartridgeBatterySaveFileServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Temporary battery save file cleanup failed."
    )]
    internal static partial void TemporarySaveFileCleanupFailed(
        ILogger logger,
        Exception exception
    );
}
