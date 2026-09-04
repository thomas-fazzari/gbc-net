// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using GbcNet.App.Shell.Chrome;

namespace GbcNet.Tests.Unit.App.Shell.Chrome;

public sealed class ShellPresenterTests
{
    [Fact]
    public void ShowError_SplitsMessagesAndShowsNotification()
    {
        var notification = new Border { IsVisible = false };
        var items = new ItemsControl();
        var presenter = CreatePresenter(notification: notification, notificationItems: items);

        presenter.ShowError($"First{Environment.NewLine}Second");

        notification.IsVisible.Should().BeTrue();
        notification[AutomationProperties.NameProperty]
            .Should()
            .Be($"First{Environment.NewLine}Second");

        items.ItemsSource!.Cast<string>().Should().Equal("First", "Second");
    }

    [Fact]
    public void ShowEmulationState_UsesPausedStateBeforeEffectiveSpeed()
    {
        var state = new TextBlock();
        var pause = new ToggleButton();
        var fastForward = new ToggleButton();
        var presenter = CreatePresenter(state, pause, fastForward);

        presenter.ShowEmulationState(
            hasSession: true,
            isPaused: true,
            fastForwardEnabled: true,
            effectiveSpeed: "4x"
        );

        state.Text.Should().Be("Paused");
        state.IsVisible.Should().BeTrue();
        pause.IsChecked.Should().BeTrue();
        fastForward.IsChecked.Should().BeTrue();
    }

    [Theory]
    [InlineData("Pokemon - Crystal Version (USA, Europe) (Rev 1).gbc", "Pokemon - Crystal Version")]
    [InlineData("Game (USA).gbc", "Game")]
    [InlineData("Game (USA) extra.gbc", "Game (USA) extra")]
    [InlineData("Game.gbc", "Game")]
    public void ShowRomFileName_StripsOnlyTrailingFilenameTags(
        string fileName,
        string expectedTitle
    )
    {
        var title = new TextBlock();
        var presenter = CreatePresenter(romTitle: title);

        presenter.ShowRomFileName(fileName);

        title.Text.Should().Be(expectedTitle);
    }

    [Theory]
    [InlineData(false, "1x", "1×", false)]
    [InlineData(true, "8x", "8×", true)]
    public void ShowEmulationState_HidesNormalSpeedLabel(
        bool fastForwardEnabled,
        string effectiveSpeed,
        string expectedLabel,
        bool expectedVisible
    )
    {
        var state = new TextBlock();
        var presenter = CreatePresenter(state: state);

        presenter.ShowEmulationState(
            hasSession: true,
            isPaused: false,
            fastForwardEnabled: fastForwardEnabled,
            effectiveSpeed: effectiveSpeed
        );

        state.Text.Should().Be(expectedLabel);
        state.IsVisible.Should().Be(expectedVisible);
    }

    private static ShellPresenter CreatePresenter(
        TextBlock? state = null,
        ToggleButton? pause = null,
        ToggleButton? fastForward = null,
        Border? notification = null,
        ItemsControl? notificationItems = null,
        TextBlock? romTitle = null
    ) =>
        new(
            romTitle ?? new TextBlock(),
            state ?? new TextBlock(),
            pause ?? new ToggleButton(),
            fastForward ?? new ToggleButton(),
            notification ?? new Border(),
            notificationItems ?? new ItemsControl()
        );
}
