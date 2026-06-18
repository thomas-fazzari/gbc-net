using FluentResults;
using GbcNet.Core;
using KdlSharp;

namespace GbcNet.App.Configuration;

/// <summary>
/// Reads optional boot ROM paths from the KDL configuration document.
/// </summary>
internal static class KdlBootRomOptionsReader
{
    private const string BootRomNodeName = "boot_roms";
    private const string DmgNodeName = "dmg";
    private const string CgbNodeName = "cgb";

    public static Result<GameBoyOptions> Read(KdlDocument document, string configDirectoryPath)
    {
        KdlNode? bootRomNode = null;

        foreach (var node in document.Nodes)
        {
            if (!string.Equals(node.Name, BootRomNodeName, StringComparison.Ordinal))
            {
                continue;
            }

            if (bootRomNode is not null)
            {
                return Result.Fail("Config file must contain only one boot_roms node.");
            }

            bootRomNode = node;
        }

        return bootRomNode is null
            ? new GameBoyOptions()
            : ReadBootRomNode(bootRomNode, configDirectoryPath);
    }

    private static Result<GameBoyOptions> ReadBootRomNode(
        KdlNode bootRomNode,
        string configDirectoryPath
    )
    {
        ReadOnlyMemory<byte> dmgBootRom = default;
        ReadOnlyMemory<byte> cgbBootRom = default;

        foreach (var node in bootRomNode.Children)
        {
            if (node.Children.Count != 0)
            {
                return Result.Fail($"Boot ROM config node '{node.Name}' must not have children.");
            }

            switch (node.Name)
            {
                case DmgNodeName:
                    if (!dmgBootRom.IsEmpty)
                    {
                        return Result.Fail("Boot ROM config has duplicate dmg node.");
                    }

                    var dmg = ReadBootRom(
                        node,
                        configDirectoryPath,
                        "DMG",
                        GameBoyOptions.DmgBootRomSize
                    );
                    if (dmg.IsFailed)
                    {
                        return dmg.ToResult<GameBoyOptions>();
                    }

                    dmgBootRom = dmg.Value;
                    break;

                case CgbNodeName:
                    if (!cgbBootRom.IsEmpty)
                    {
                        return Result.Fail("Boot ROM config has duplicate cgb node.");
                    }

                    var cgb = ReadBootRom(
                        node,
                        configDirectoryPath,
                        "CGB",
                        GameBoyOptions.CgbBootRomSize
                    );
                    if (cgb.IsFailed)
                    {
                        return cgb.ToResult<GameBoyOptions>();
                    }

                    cgbBootRom = cgb.Value;
                    break;

                default:
                    return Result.Fail(
                        $"Boot ROM config node '{BootRomNodeName}' does not allow child '{node.Name}'."
                    );
            }
        }

        return new GameBoyOptions { DmgBootRom = dmgBootRom, CgbBootRom = cgbBootRom };
    }

    private static Result<byte[]> ReadBootRom(
        KdlNode node,
        string configDirectoryPath,
        string label,
        int expectedLength
    )
    {
        var path = KdlNodeReader.ReadRequiredStringArgument(node);
        if (path.IsFailed)
        {
            return path.ToResult<byte[]>();
        }

        if (string.IsNullOrWhiteSpace(path.Value))
        {
            return Result.Fail($"{label} boot ROM path must not be empty.");
        }

        var resolvedPath = Path.IsPathFullyQualified(path.Value)
            ? path.Value
            : Path.GetFullPath(Path.Combine(configDirectoryPath, path.Value));

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(resolvedPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"{label} boot ROM file could not be read: {exception.Message}");
        }

        return bytes.Length == expectedLength
            ? bytes
            : Result.Fail(
                $"{label} boot ROM must be {expectedLength} bytes, but was {bytes.Length} bytes."
            );
    }
}
