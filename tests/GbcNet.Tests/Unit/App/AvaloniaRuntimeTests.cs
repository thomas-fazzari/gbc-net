// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;

namespace GbcNet.Tests.Unit.App;

public sealed class AvaloniaRuntimeTests
{
    [Theory]
    [InlineData("Avalonia.Native")]
    [InlineData("Avalonia.Win32")]
    [InlineData("Avalonia.X11")]
    public void DesktopBackendTypes_AreCompatibleWithAvalonia(string assemblyName)
    {
        FluentActions.Invoking(() => Assembly.Load(assemblyName).GetTypes()).Should().NotThrow();
    }
}
