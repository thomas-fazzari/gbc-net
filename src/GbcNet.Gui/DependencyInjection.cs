using GbcNet.Gui.Audio;
using GbcNet.Gui.Configuration;
using GbcNet.Gui.Input;
using GbcNet.Gui.Input.Configuration;
using GbcNet.Gui.Saves;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GbcNet.Gui;

/// <summary>
/// Registers GUI services and resolves startup configuration.
/// </summary>
internal static class DependencyInjection
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IValidateOptions<InputOptions>, InputOptionsValidator>();
        services.AddSingleton(provider =>
            StartupConfigurationLoader.Load(
                provider.GetRequiredService<IValidateOptions<InputOptions>>(),
                KdlConfigurationFile.UserConfigPath
            )
        );
        services
            .AddOptions<InputOptions>()
            .Configure<StartupConfiguration>(
                static (options, startupConfiguration) =>
                {
                    InputOptions inputOptions = startupConfiguration.InputOptions;
                    options.Version = inputOptions.Version;
                    options.ActiveProfile = inputOptions.ActiveProfile;
                    options.Profiles = inputOptions.Profiles;
                }
            );
        services.AddSingleton<InputConfiguration>(provider =>
            InputConfiguration.FromOptions(
                provider.GetRequiredService<IOptions<InputOptions>>().Value
            )
        );
        services.AddSingleton(_ => new CartridgeSaveFileService(
            CartridgeSaveFileService.UserSaveDirectoryPath
        ));
        services.AddSingleton<IAudioOutput, SoundFlowAudioOutput>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
