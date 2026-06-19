using FluentResults;
using GbcNet.App.Configuration.Kdl;
using GbcNet.Core;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.BootRom;

/// <summary>
/// Reads optional boot ROM paths from the KDL configuration document.
/// </summary>
internal static class BootRomOptionsReader
{
    public static Result<GameBoyOptions> Read(KdlDocument document, string configDirectoryPath)
    {
        var bootRomNode = document.ReadOptionalSection(BootRomOptionsSchema.BootRomNodeName);
        if (bootRomNode.IsFailed)
        {
            return bootRomNode.ToResult<GameBoyOptions>();
        }

        return bootRomNode.Value is null
            ? new GameBoyOptions()
            : ReadBootRomNode(bootRomNode.Value, configDirectoryPath);
    }

    public static Result<BootRomPathOptions> ReadPaths(KdlDocument document)
    {
        var bootRomNode = document.ReadOptionalSection(BootRomOptionsSchema.BootRomNodeName);
        if (bootRomNode.IsFailed)
        {
            return bootRomNode.ToResult<BootRomPathOptions>();
        }

        return bootRomNode.Value is null
            ? new BootRomPathOptions()
            : ReadPathOptionsNode(bootRomNode.Value);
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
                case BootRomOptionsSchema.DmgNodeName:
                    if (!dmgBootRom.IsEmpty)
                    {
                        return Result.Fail("Boot ROM config has duplicate dmg node.");
                    }

                    var dmg = ReadBootRom(
                        node,
                        configDirectoryPath,
                        BootRomOptionsSchema.DmgNodeName.ToUpperInvariant(),
                        GameBoyOptions.DmgBootRomSize
                    );
                    if (dmg.IsFailed)
                    {
                        return dmg.ToResult<GameBoyOptions>();
                    }

                    dmgBootRom = dmg.Value;
                    break;

                case BootRomOptionsSchema.CgbNodeName:
                    if (!cgbBootRom.IsEmpty)
                    {
                        return Result.Fail("Boot ROM config has duplicate cgb node.");
                    }

                    var cgb = ReadBootRom(
                        node,
                        configDirectoryPath,
                        BootRomOptionsSchema.CgbNodeName.ToUpperInvariant(),
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
                        $"Boot ROM config node '{BootRomOptionsSchema.BootRomNodeName}' does not allow child '{node.Name}'."
                    );
            }
        }

        return new GameBoyOptions { DmgBootRom = dmgBootRom, CgbBootRom = cgbBootRom };
    }

    private static Result<BootRomPathOptions> ReadPathOptionsNode(KdlNode bootRomNode)
    {
        string? dmgPath = null;
        string? cgbPath = null;

        foreach (var node in bootRomNode.Children)
        {
            if (node.Children.Count != 0)
            {
                return Result.Fail($"Boot ROM config node '{node.Name}' must not have children.");
            }

            switch (node.Name)
            {
                case BootRomOptionsSchema.DmgNodeName:
                    if (dmgPath is not null)
                    {
                        return Result.Fail("Boot ROM config has duplicate dmg node.");
                    }

                    var dmg = KdlNodeReader.ReadRequiredStringArgument(node);
                    if (dmg.IsFailed)
                    {
                        return dmg.ToResult<BootRomPathOptions>();
                    }

                    dmgPath = dmg.Value;
                    break;

                case BootRomOptionsSchema.CgbNodeName:
                    if (cgbPath is not null)
                    {
                        return Result.Fail("Boot ROM config has duplicate cgb node.");
                    }

                    var cgb = KdlNodeReader.ReadRequiredStringArgument(node);
                    if (cgb.IsFailed)
                    {
                        return cgb.ToResult<BootRomPathOptions>();
                    }

                    cgbPath = cgb.Value;
                    break;

                default:
                    return Result.Fail(
                        $"Boot ROM config node '{BootRomOptionsSchema.BootRomNodeName}' does not allow child '{node.Name}'."
                    );
            }
        }

        return new BootRomPathOptions(dmgPath, cgbPath);
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

        byte[] bytes;
        try
        {
            var resolvedPath = Path.IsPathFullyQualified(path.Value)
                ? path.Value
                : Path.GetFullPath(Path.Combine(configDirectoryPath, path.Value));
            bytes = File.ReadAllBytes(resolvedPath);
        }
        catch (Exception exception) when (IsExpectedPathException(exception))
        {
            return Result.Fail($"{label} boot ROM file could not be read: {exception.Message}");
        }

        return bytes.Length == expectedLength
            ? bytes
            : Result.Fail(
                $"{label} boot ROM must be {expectedLength} bytes, but was {bytes.Length} bytes."
            );
    }

    private static bool IsExpectedPathException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
}
