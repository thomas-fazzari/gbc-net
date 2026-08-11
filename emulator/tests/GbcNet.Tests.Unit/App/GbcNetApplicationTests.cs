// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Styling;
using GbcNet.App;
using GbcNet.App.Configuration.Sections.Appearance;

namespace GbcNet.Tests.Unit.App;

public sealed class GbcNetApplicationTests
{
    [Theory]
    [InlineData((int)ThemeMode.System)]
    [InlineData((int)ThemeMode.Light)]
    [InlineData((int)ThemeMode.Dark)]
    public void GetThemeVariant_MapsThemeMode(int value)
    {
        var mode = (ThemeMode)value;
        var expected = mode switch
        {
            ThemeMode.System => ThemeVariant.Default,
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, message: null),
        };

        GbcNetApplication.GetThemeVariant(mode).Should().Be(expected);
    }
}
