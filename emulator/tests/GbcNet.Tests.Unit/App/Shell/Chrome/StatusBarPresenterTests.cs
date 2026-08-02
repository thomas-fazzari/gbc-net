// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.Unit.App.Shell.Chrome;

public sealed class StatusBarPresenterTests
{
    [Fact]
    public void FormatRomFileName_PreservesLongNamesForXamlTrimming()
    {
        var fileName = new string('A', 80) + ".gb";

        StatusBarPresenter.FormatRomFileName(fileName).Should().Be(new string('A', 80));
    }

    [Fact]
    public void FormatHardwareModel_UsesUppercaseModelName()
    {
        StatusBarPresenter.FormatHardwareModel(HardwareModel.Sgb).Should().Be("SGB");
    }

    [Fact]
    public void ShowSpeed_UpdatesTextAndTogglesBadgeVisibility()
    {
        var speedBadge = new Border();
        var speedText = new TextBlock();
        var presenter = CreatePresenter(speedBadge, speedText);
        try
        {
            presenter.ShowSpeed("2x");

            speedText.Text.Should().Be("2x");
            speedBadge.IsVisible.Should().BeTrue();

            presenter.ShowSpeed(string.Empty);

            speedText.Text.Should().Be(string.Empty);
            speedBadge.IsVisible.Should().BeFalse();
        }
        finally
        {
            presenter.Dispose();
        }
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
