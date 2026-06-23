using FluentResults;
using GbcNet.App.Configuration.Kdl;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.BootRom;

/// <summary>
/// Writes boot ROM path settings into the KDL configuration file.
/// </summary>
internal static class BootRomConfigWriter
{
    public static Result Write(string configPath, BootRomConfig config)
    {
        var text = KdlConfigurationFile.LoadTextOrCreate(configPath);
        if (text.IsFailed)
        {
            return text.ToResult();
        }

        var document = KdlConfigurationFile.Parse(text.Value);
        if (document.IsFailed)
        {
            return document.ToResult();
        }

        var section = document.Value.ReadOptionalSection(BootRomConfigSchema.BootRomNodeName);
        if (section.IsFailed)
        {
            return section.ToResult();
        }

        var replacement = KdlSectionTextEditor.ReplaceTopLevelSection(
            text.Value,
            section.Value,
            CreateBootRomNode(config).ToKdlString()
        );

        return replacement.IsFailed
            ? replacement.ToResult()
            : KdlConfigurationFile.SaveText(configPath, replacement.Value);
    }

    private static KdlNode CreateBootRomNode(BootRomConfig config)
    {
        var bootRomNode = new KdlNode(BootRomConfigSchema.BootRomNodeName);
        AddPath(bootRomNode, BootRomConfigSchema.DmgNodeName, config.DmgPath);
        AddPath(bootRomNode, BootRomConfigSchema.CgbNodeName, config.CgbPath);
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

internal readonly record struct BootRomConfig(string? DmgPath, string? CgbPath);
