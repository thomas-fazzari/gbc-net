// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Audio;

internal static class DependencyInjection
{
    [DependencyInjectionModule]
    public static IServiceCollection AddAudio(this IServiceCollection services)
    {
        services.AddSingleton<IAudioOutput, SoundFlowAudioOutput>();
        return services;
    }
}
