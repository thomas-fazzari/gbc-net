// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text;
using FluentResults;
using GbcNet.Core.Cartridges;

namespace GbcNet.App.Saves;

/// <summary>
/// Persists cartridge battery-backed save data under the configured save directory.
/// </summary>
internal sealed class CartridgeBatterySaveFileService
{
    private const string SaveFileExtension = ".sav";
    private const int ShortHashHexLength = 8;
    private const string FallbackSaveName = "GAME";

    private readonly string _saveDirectoryPath;

    internal CartridgeBatterySaveFileService(string saveDirectoryPath)
    {
        _saveDirectoryPath = saveDirectoryPath;
    }

    public Result Load(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        if (!cartridge.HasBatteryBackedSave)
        {
            return Result.Ok();
        }

        var path = GetBatterySavePath(cartridge, rom);
        if (!File.Exists(path))
        {
            return Result.Ok();
        }

        try
        {
            var saveLength = new FileInfo(path).Length;
            if (saveLength != cartridge.BatterySaveSize)
            {
                return Result.Fail(
                    $"Save file is {saveLength} bytes, but cartridge expects {cartridge.BatterySaveSize} bytes."
                );
            }

            return cartridge.ImportBatterySave(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail("Save file could not be read: " + exception.Message);
        }
    }

    public Result Save(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        if (!cartridge.HasBatteryBackedSave || !cartridge.IsBatterySaveDirty)
        {
            return Result.Ok();
        }

        try
        {
            Directory.CreateDirectory(_saveDirectoryPath);
            var savePath = GetBatterySavePath(cartridge, rom);
            var temporaryPath = $"{savePath}.{Guid.NewGuid():N}.tmp";

            File.WriteAllBytes(temporaryPath, cartridge.ExportBatterySave());
            File.Move(temporaryPath, savePath, overwrite: true);
            cartridge.ClearBatterySaveDirty();
            return Result.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail("Save file could not be written: " + exception.Message);
        }
    }

    internal string GetBatterySavePath(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        var hash = SHA256.HashData(rom);
        var fileName = string.Concat(
            SanitizeName(cartridge.Header.Title),
            "-",
            Convert.ToHexString(hash.AsSpan(0, ShortHashHexLength / 2)),
            SaveFileExtension
        );

        return Path.Combine(_saveDirectoryPath, fileName);
    }

    private static string SanitizeName(string name)
    {
        StringBuilder builder = new(name.Length);

        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
            else if (character is ' ' or '-' or '_')
            {
                builder.Append('_');
            }
        }

        return builder.Length == 0 ? FallbackSaveName : builder.ToString();
    }
}
