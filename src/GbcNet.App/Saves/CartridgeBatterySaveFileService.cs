// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.Core.Cartridges;

namespace GbcNet.App.Saves;

/// <summary>
/// Persists cartridge battery-backed save data under the configured save directory.
/// </summary>
internal sealed class CartridgeBatterySaveFileService
{
    private const string SaveFileExtension = ".sav";

    private readonly string _saveDirectoryPath;

    internal CartridgeBatterySaveFileService(string saveDirectoryPath)
    {
        _saveDirectoryPath = saveDirectoryPath;
    }

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
                throw new InvalidOperationException(
                    errorMessage ?? "Save file could not be imported."
                );
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
        try
        {
            Directory.CreateDirectory(_saveDirectoryPath);
            var temporaryPath = $"{savePath}.{Guid.NewGuid():N}.tmp";

            await File.WriteAllBytesAsync(temporaryPath, save, CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);
            File.Move(sourceFileName: temporaryPath, destFileName: savePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                message: "Save file could not be written: " + exception.Message,
                innerException: exception
            );
        }
    }

    internal string GetBatterySavePath(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);

        return Path.Combine(
            path1: _saveDirectoryPath,
            path2: identity.FileStem + SaveFileExtension
        );
    }
}
