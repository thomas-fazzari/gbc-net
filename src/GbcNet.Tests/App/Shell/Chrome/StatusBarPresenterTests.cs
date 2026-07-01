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
}
