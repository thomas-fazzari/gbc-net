// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;

namespace GbcNet.Tests.Unit.App.Configuration;

public sealed class UserDataPathsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/path")]
    public void GetXdgDirectoryPath_InvalidConfiguredPathUsesAbsoluteFallback(
        string? configuredPath
    )
    {
        var userProfilePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "gbcnet-home"));

        var path = UserDataPaths.GetXdgDirectoryPath(
            configuredPath,
            userProfilePath,
            fallbackDirectoryName: ".config"
        );

        path.Should().Be(Path.Combine(userProfilePath, ".config"));
        Path.IsPathFullyQualified(path).Should().BeTrue();
    }

    [Fact]
    public void GetXdgDirectoryPath_AbsoluteConfiguredPathIsNormalized()
    {
        var rootPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "gbcnet-xdg"));
        var configuredPath = Path.Combine(rootPath, "nested", "..", "config");

        var path = UserDataPaths.GetXdgDirectoryPath(
            configuredPath,
            userProfilePath: Path.Combine(rootPath, "home"),
            fallbackDirectoryName: ".config"
        );

        path.Should().Be(Path.Combine(rootPath, "config"));
        Path.IsPathFullyQualified(path).Should().BeTrue();
    }
}
