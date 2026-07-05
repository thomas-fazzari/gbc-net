// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;

namespace GbcNet.Tests.App.EmulationState;

public sealed class EmulationSpeedTests
{
    [Theory]
    [InlineData(10, "1x")]
    [InlineData(15, "1.5x")]
    [InlineData(20, "2x")]
    [InlineData(25, "2.5x")]
    [InlineData(30, "3x")]
    [InlineData(35, "3.5x")]
    [InlineData(40, "4x")]
    [InlineData(80, "8x")]
    public void GetDisplayName_ReturnsExpectedLabel(int speed, string expected)
    {
        Assert.Equal(expected, ((EmulationSpeed)speed).GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_RejectsUnsupportedSpeed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((EmulationSpeed)999).GetDisplayName());
    }
}
