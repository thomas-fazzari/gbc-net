using System.Globalization;
using Avalonia.Controls;

namespace GbcNet.App.Chrome;

internal sealed class StatusBarPresenter(TextBlock message, TextBlock metrics)
{
    private const int RomFileNameMaxLength = 72;

    public void ShowStatus(string text)
    {
        message.Foreground = AppChrome.Brush(AppChrome.Status);
        message.Text = text;
        metrics.Text = string.Empty;
    }

    public void ShowError(string text)
    {
        message.Foreground = AppChrome.Brush(AppChrome.Error);
        message.Text = text;
        metrics.Text = string.Empty;
    }

    public void ShowRomFileName(string romFileName)
    {
        ShowStatus(FormatRomFileName(romFileName));
    }

    public void ShowMetrics(double speedMultiplier, double renderedFramesPerSecond)
    {
        metrics.Text = FormatMetrics(speedMultiplier, renderedFramesPerSecond);
    }

    internal static string FormatMetrics(double speedMultiplier, double renderedFramesPerSecond) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Speed {speedMultiplier:0.#}x | {renderedFramesPerSecond:0} FPS"
        );

    internal static string FormatRomFileName(string romFileName) =>
        romFileName.Length <= RomFileNameMaxLength
            ? romFileName
            : $"{romFileName.AsSpan(0, RomFileNameMaxLength - 3)}...";
}
