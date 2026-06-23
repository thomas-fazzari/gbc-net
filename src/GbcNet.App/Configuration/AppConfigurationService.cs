using FluentResults;
using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed class AppConfigurationService(string configPath)
{
    public Result<BootRomConfig> LoadBootRomConfig()
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);
        return document.IsFailed
            ? Result.Fail<BootRomConfig>(document.Errors)
            : BootRomConfigReader.ReadConfig(document.Value);
    }

    public Result<BootRomOptions> LoadBootRomOptions()
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);
        return document.IsFailed
            ? Result.Fail<BootRomOptions>(document.Errors)
            : BootRomConfigReader.ReadBootRomOptions(
                document.Value,
                Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
            );
    }

    public Result SaveBootRomConfig(BootRomConfig config) =>
        BootRomConfigWriter.Write(configPath, config);
}
