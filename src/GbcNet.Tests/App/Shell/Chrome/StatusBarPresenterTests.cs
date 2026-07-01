using GbcNet.App.Shell.Chrome;

namespace GbcNet.Tests.App.Chrome;

public sealed class StatusBarPresenterTests
{
    [Fact]
    public void FormatRomFileName_KeepsShortNames()
    {
        Assert.Equal("Tetris.gb", StatusBarPresenter.FormatRomFileName("Tetris.gb"));
    }

    [Fact]
    public void FormatRomFileName_TruncatesLongNamesToStatusLimit()
    {
        var formatted = StatusBarPresenter.FormatRomFileName(new string('A', 80));

        Assert.Equal(72, formatted.Length);
        Assert.EndsWith("...", formatted, StringComparison.Ordinal);
    }
}
