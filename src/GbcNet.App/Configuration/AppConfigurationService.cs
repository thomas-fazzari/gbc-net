using FluentResults;
using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed class AppConfigurationService(string configPath)
{
    public Result<BootRomPathOptions> LoadBootRomPaths()
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);
        return document.IsFailed
            ? Result.Fail<BootRomPathOptions>(document.Errors)
            : BootRomOptionsReader.ReadPaths(document.Value);
    }

    public Result<GameBoyOptions> LoadGameBoyOptions()
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);
        return document.IsFailed
            ? Result.Fail<GameBoyOptions>(document.Errors)
            : BootRomOptionsReader.Read(
                document.Value,
                Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
            );
    }

    public Result SaveBootRomPaths(BootRomPathOptions options) =>
        BootRomOptionsWriter.Write(configPath, options);
}
