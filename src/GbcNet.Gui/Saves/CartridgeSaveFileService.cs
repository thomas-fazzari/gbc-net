using System.Security.Cryptography;
using System.Text;
using FluentResults;
using GbcNet.Core.Cartridges;

namespace GbcNet.Gui.Saves;

/// <summary>
/// Persists cartridge battery-backed RAM under the per-user application data folder.
/// </summary>
internal sealed class CartridgeSaveFileService
{
    private const string SaveDirectoryName = "saves";
    private const string SaveFileExtension = ".sav";
    private const int ShortHashHexLength = 8;
    private const string FallbackSaveName = "GAME";

    private readonly string _saveDirectoryPath;

    internal CartridgeSaveFileService(string saveDirectoryPath)
    {
        _saveDirectoryPath = saveDirectoryPath;
    }

    public static string UserSaveDirectoryPath { get; } = GetUserSaveDirectoryPath();

    public Result Load(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        if (!cartridge.HasBatteryBackedRam)
        {
            return Result.Ok();
        }

        string path = GetSavePath(cartridge, rom);
        if (!File.Exists(path))
        {
            return Result.Ok();
        }

        try
        {
            return cartridge.ImportBatteryRam(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail("Save file could not be read: " + exception.Message);
        }
    }

    public Result Save(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        if (!cartridge.HasBatteryBackedRam || !cartridge.IsBatteryRamDirty)
        {
            return Result.Ok();
        }

        try
        {
            Directory.CreateDirectory(_saveDirectoryPath);
            string savePath = GetSavePath(cartridge, rom);
            string temporaryPath = string.Concat(
                savePath,
                ".",
                Guid.NewGuid().ToString("N"),
                ".tmp"
            );

            File.WriteAllBytes(temporaryPath, cartridge.ExportBatteryRam());
            File.Move(temporaryPath, savePath, overwrite: true);
            cartridge.ClearBatteryRamDirty();
            return Result.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail("Save file could not be written: " + exception.Message);
        }
    }

    internal string GetSavePath(Cartridge cartridge, ReadOnlySpan<byte> rom)
    {
        byte[] hash = SHA256.HashData(rom);
        string fileName = string.Concat(
            SanitizeName(cartridge.Header.Title),
            "-",
            Convert.ToHexString(hash.AsSpan(0, ShortHashHexLength / 2)),
            SaveFileExtension
        );

        return Path.Combine(_saveDirectoryPath, fileName);
    }

    private static string SanitizeName(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (char character in name)
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

    private static string GetUserSaveDirectoryPath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                ApplicationDirectoryNames.Desktop,
                SaveDirectoryName
            );
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationDirectoryNames.Desktop,
                SaveDirectoryName
            );
        }

        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string dataHome = string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share"
            )
            : xdgDataHome;

        return Path.Combine(dataHome, ApplicationDirectoryNames.Linux, SaveDirectoryName);
    }
}
