// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using GbcNet.App.Audio;
using GbcNet.App.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GbcNet.App;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class DependencyInjectionModuleAttribute : Attribute;

internal static class DependencyInjection
{
    public static ServiceProvider BuildServiceProvider(StartupConfiguration startupConfiguration)
    {
        var services = new ServiceCollection();
        services.AddLogging(static builder => builder.AddDebug());
        services.AddSingleton(startupConfiguration);
        services.AddSingleton(_ => new AppConfigurationService(startupConfiguration.ConfigPath));
        services.AddSingleton<IAudioOutput, SoundFlowAudioOutput>();

        foreach (var module in DiscoverModules())
        {
            module.Invoke(null, [services]);
        }

        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider();
    }

    private static IEnumerable<MethodInfo> DiscoverModules() =>
        typeof(DependencyInjection)
            .Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public))
            .Where(method =>
                Attribute.IsDefined(method, typeof(DependencyInjectionModuleAttribute))
            )
            .OrderBy(method => method.DeclaringType?.FullName, StringComparer.Ordinal)
            .ThenBy(method => method.Name, StringComparer.Ordinal)
            .Select(ValidateModule);

    private static MethodInfo ValidateModule(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (
            method.ReturnType != typeof(IServiceCollection)
            || parameters is not [{ ParameterType: var parameterType }]
            || parameterType != typeof(IServiceCollection)
        )
        {
            throw new InvalidOperationException(
                $"Dependency injection module '{method.DeclaringType?.FullName}.{method.Name}' must be static IServiceCollection Method(IServiceCollection services)."
            );
        }

        return method;
    }
}
