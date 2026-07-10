// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.App.Shell.Chrome;

public sealed class StatusBarPresenterTests
{
    [Fact]
    public void FormatRomFileName_KeepsShortNames()
    {
        Assert.Equal("Tetris", StatusBarPresenter.FormatRomFileName("Tetris.gb"));
    }

    [Fact]
    public void FormatRomFileName_TruncatesLongNamesToStatusLimit()
    {
        var formatted = StatusBarPresenter.FormatRomFileName(new string('A', 80));

        Assert.Equal(72, formatted.Length);
        Assert.EndsWith("...", formatted, StringComparison.Ordinal);
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
