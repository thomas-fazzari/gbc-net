using GbcNet.App.Audio;
using GbcNet.App.Configuration;
using GbcNet.App.Input;
using GbcNet.App.Input.Configuration;
using GbcNet.App.Saves;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GbcNet.App;

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
                UserDataPaths.ConfigFilePath
            )
        );
        services
            .AddOptions<InputOptions>()
            .Configure<StartupConfiguration>(
                static (options, startupConfiguration) =>
                {
                    var inputOptions = startupConfiguration.InputOptions;
                    options.Version = inputOptions.Version;
                    options.ActiveProfile = inputOptions.ActiveProfile;
                    options.Profiles = inputOptions.Profiles;
                }
            );
        services.AddSingleton(provider =>
            InputConfiguration.FromOptions(
                provider.GetRequiredService<IOptions<InputOptions>>().Value
            )
        );
        services.AddSingleton(_ => new CartridgeSaveFileService(UserDataPaths.SaveDirectoryPath));
        services.AddSingleton<IAudioOutput, SoundFlowAudioOutput>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
