// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Kdl;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.BootRom;

/// <summary>
/// Writes boot ROM path settings into the KDL configuration file.
/// </summary>
internal static class BootRomConfigWriter
{
    public static void Write(string configPath, BootRomConfig config)
    {
        var text = KdlConfigurationFile.LoadTextOrCreate(configPath);
        var document = KdlConfigurationFile.Parse(text);
        var replacement = KdlSectionTextEditor.ReplaceTopLevelSection(
            text,
            document.ReadOptionalSection(BootRomConfigSchema.BootRomNodeName),
            CreateBootRomNode(config).ToKdlString()
        );

        KdlConfigurationFile.SaveText(configPath, replacement);
    }

    private static KdlNode CreateBootRomNode(BootRomConfig config)
    {
        var bootRomNode = new KdlNode(BootRomConfigSchema.BootRomNodeName);
        AddPath(bootRomNode, BootRomConfigSchema.DmgNodeName, config.DmgPath);
        AddPath(bootRomNode, BootRomConfigSchema.CgbNodeName, config.CgbPath);
        AddPath(bootRomNode, BootRomConfigSchema.SgbNodeName, config.SgbPath);
        return bootRomNode;
    }

    private static void AddPath(KdlNode bootRomNode, string nodeName, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            bootRomNode.AddChild(new KdlNode(nodeName).AddArgument(path));
        }
    }
}

internal readonly record struct BootRomConfig(
    string? DmgPath = null,
    string? CgbPath = null,
    string? SgbPath = null
);
