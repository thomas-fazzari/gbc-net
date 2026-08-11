// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Styling;
using GbcNet.App;
using GbcNet.App.Configuration.Sections.Appearance;

namespace GbcNet.Tests.Unit.App;

public sealed class GbcNetApplicationTests
{
    [Fact]
    public void GetThemeVariant_MapsThemeModes()
    {
        GbcNetApplication.GetThemeVariant(ThemeMode.System).Should().Be(ThemeVariant.Default);
        GbcNetApplication.GetThemeVariant(ThemeMode.Light).Should().Be(ThemeVariant.Light);
        GbcNetApplication.GetThemeVariant(ThemeMode.Dark).Should().Be(ThemeVariant.Dark);
    }
}
