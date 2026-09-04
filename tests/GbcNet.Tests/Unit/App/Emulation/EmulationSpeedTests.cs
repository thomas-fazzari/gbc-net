// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;

namespace GbcNet.Tests.Unit.App.Emulation;

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
        ((EmulationSpeed)speed).GetDisplayName().Should().Be(expected);
    }

    [Fact]
    public void GetDisplayName_RejectsUnsupportedSpeed()
    {
        FluentActions
            .Invoking(() => ((EmulationSpeed)999).GetDisplayName())
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }
}
