// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using GbcNet.App.Infrastructure.Configuration;
using GbcNet.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.Tests.Unit.App;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void BuildServiceProvider_WithCurrentRegistrations_Succeeds()
    {
        var config = AppConfigurationFile.CreateDefault();
        var startupConfiguration = new StartupConfiguration(
            config.Input,
            config.Emulation,
            config.Audio,
            config.Library,
            new BootRomOptions(),
            ConfigPath: "config.json",
            StartupErrorMessage: null
        );

        using var provider = GbcNet.App.DependencyInjection.BuildServiceProvider(
            startupConfiguration
        );

        Assert.Same(startupConfiguration, provider.GetRequiredService<StartupConfiguration>());
    }
}
