using FluentResults;
using GbcNet.App.Chrome;

namespace GbcNet.Tests.App.Chrome;

public sealed class StatusBarPresenterTests
{
    [Fact]
    public void FormatRomName_KeepsShortNames()
    {
        Assert.Equal("Tetris.gb", StatusBarPresenter.FormatRomName("Tetris.gb"));
    }

    [Fact]
    public void FormatRomName_TruncatesLongNamesToStatusLimit()
    {
        var formatted = StatusBarPresenter.FormatRomName(new string('A', 80));

        Assert.Equal(72, formatted.Length);
        Assert.EndsWith("...", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatErrors_JoinsErrorMessages()
    {
        var formatted = StatusBarPresenter.FormatErrors([new Error("first"), new Error("second")]);

        Assert.Equal($"first{Environment.NewLine}second", formatted);
    }

    [Fact]
    public void FormatMetrics_UsesInvariantDisplay()
    {
        Assert.Equal("1.5x | 60 fps", StatusBarPresenter.FormatMetrics(1.5, 59.8));
    }
}
