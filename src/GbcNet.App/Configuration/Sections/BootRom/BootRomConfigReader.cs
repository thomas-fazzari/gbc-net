// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Kdl;
using GbcNet.Core;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.BootRom;

/// <summary>
/// Reads optional boot ROM paths from the KDL configuration document.
/// </summary>
internal static class BootRomConfigReader
{
    public static BootRomOptions ReadBootRomOptions(
        KdlDocument document,
        string configDirectoryPath
    )
    {
        var config = ReadConfig(document);

        return new BootRomOptions
        {
            DmgBootRom = ReadBootRom(
                config.DmgPath,
                configDirectoryPath,
                "DMG",
                BootRomOptions.DmgBootRomSize
            ),
            CgbBootRom = ReadBootRom(
                config.CgbPath,
                configDirectoryPath,
                "CGB",
                BootRomOptions.CgbBootRomSize
            ),
            SgbBootRom = ReadBootRom(
                config.SgbPath,
                configDirectoryPath,
                "SGB",
                BootRomOptions.SgbBootRomSize
            ),
        };
    }

    public static BootRomConfig ReadConfig(KdlDocument document)
    {
        var bootRomNode = document.ReadOptionalSection(BootRomConfigSchema.BootRomNodeName);
        return bootRomNode is null ? new BootRomConfig() : ReadConfigNode(bootRomNode);
    }

    private static BootRomConfig ReadConfigNode(KdlNode bootRomNode)
    {
        string? dmgPath = null;
        string? cgbPath = null;
        string? sgbPath = null;

        foreach (var node in bootRomNode.Children)
        {
            if (node.Children.Count != 0)
            {
                throw new ConfigurationException(
                    $"Boot ROM config node '{node.Name}' must not have children."
                );
            }

            switch (node.Name)
            {
                case BootRomConfigSchema.DmgNodeName:
                    ThrowIfDuplicate(dmgPath, node.Name);
                    dmgPath = KdlNodeReader.ReadRequiredStringArgument(node);
                    break;

                case BootRomConfigSchema.CgbNodeName:
                    ThrowIfDuplicate(cgbPath, node.Name);
                    cgbPath = KdlNodeReader.ReadRequiredStringArgument(node);
                    break;

                case BootRomConfigSchema.SgbNodeName:
                    ThrowIfDuplicate(sgbPath, node.Name);
                    sgbPath = KdlNodeReader.ReadRequiredStringArgument(node);
                    break;

                default:
                    throw new ConfigurationException(
                        $"Boot ROM config node '{BootRomConfigSchema.BootRomNodeName}' does not allow child '{node.Name}'."
                    );
            }
        }

        return new BootRomConfig(dmgPath, cgbPath, sgbPath);
    }

    private static ReadOnlyMemory<byte> ReadBootRom(
        string? path,
        string configDirectoryPath,
        string label,
        int expectedLength
    )
    {
        if (path is null)
        {
            return default;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigurationException($"{label} boot ROM path must not be empty.");
        }

        byte[] bytes;
        try
        {
            var resolvedPath = Path.IsPathFullyQualified(path)
                ? path
                : Path.GetFullPath(Path.Combine(configDirectoryPath, path));
            bytes = File.ReadAllBytes(resolvedPath);
        }
        catch (Exception exception) when (IsExpectedPathException(exception))
        {
            throw new ConfigurationException(
                $"{label} boot ROM file could not be read: {exception.Message}"
            );
        }

        return bytes.Length == expectedLength
            ? bytes
            : throw new ConfigurationException(
                $"{label} boot ROM must be {expectedLength} bytes, but was {bytes.Length} bytes."
            );
    }

    private static void ThrowIfDuplicate(string? currentPath, string nodeName)
    {
        if (currentPath is not null)
        {
            throw new ConfigurationException($"Boot ROM config has duplicate {nodeName} node.");
        }
    }

    private static bool IsExpectedPathException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
}
