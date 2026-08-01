// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.App.Shell.Chrome;

public sealed class StatusBarPresenterTests
{
    [Fact]
    public void FormatRomFileName_PreservesLongNamesForXamlTrimming()
    {
        var fileName = new string('A', 80) + ".gb";

        Assert.Equal(new string('A', 80), StatusBarPresenter.FormatRomFileName(fileName));
    }

    [Fact]
    public void FormatHardwareModel_UsesUppercaseModelName()
    {
        Assert.Equal("SGB", StatusBarPresenter.FormatHardwareModel(HardwareModel.Sgb));
    }

    [Fact]
    public void ShowSpeed_UpdatesTextAndTogglesBadgeVisibility()
    {
        var speedBadge = new Border();
        var speedText = new TextBlock();
        using var presenter = CreatePresenter(speedBadge, speedText);

        presenter.ShowSpeed("2x");

        Assert.Equal("2x", speedText.Text);
        Assert.True(speedBadge.IsVisible);

        presenter.ShowSpeed(string.Empty);

        Assert.Equal(string.Empty, speedText.Text);
        Assert.False(speedBadge.IsVisible);
    }

    private static StatusBarPresenter CreatePresenter(Border speedBadge, TextBlock speed) =>
        new(
            new TextBlock(),
            new Border(),
            new Image(),
            new Border(),
            new TextBlock(),
            speedBadge,
            speed
        );
}
