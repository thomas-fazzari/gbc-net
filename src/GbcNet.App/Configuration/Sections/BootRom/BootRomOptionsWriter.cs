using FluentResults;
using GbcNet.App.Configuration.Kdl;
using KdlSharp;

namespace GbcNet.App.Configuration.Sections.BootRom;

/// <summary>
/// Writes boot ROM path settings into the KDL configuration file.
/// </summary>
internal static class BootRomOptionsWriter
{
    public static Result Write(string configPath, BootRomPathOptions options)
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);
        if (document.IsFailed)
        {
            return document.ToResult();
        }

        var section = document.Value.ReadOptionalSection(BootRomOptionsSchema.BootRomNodeName);
        if (section.IsFailed)
        {
            return section.ToResult();
        }

        var text = KdlConfigurationFile.LoadTextOrCreate(configPath);
        if (text.IsFailed)
        {
            return text.ToResult();
        }

        var replacement = KdlSectionTextEditor.ReplaceTopLevelSection(
            text.Value,
            section.Value,
            CreateBootRomNode(options).ToKdlString()
        );

        return replacement.IsFailed
            ? replacement.ToResult()
            : KdlConfigurationFile.SaveText(configPath, replacement.Value);
    }

    private static KdlNode CreateBootRomNode(BootRomPathOptions options)
    {
        var bootRomNode = new KdlNode(BootRomOptionsSchema.BootRomNodeName);
        AddPath(bootRomNode, BootRomOptionsSchema.DmgNodeName, options.DmgPath);
        AddPath(bootRomNode, BootRomOptionsSchema.CgbNodeName, options.CgbPath);
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

internal readonly record struct BootRomPathOptions(string? DmgPath, string? CgbPath);
