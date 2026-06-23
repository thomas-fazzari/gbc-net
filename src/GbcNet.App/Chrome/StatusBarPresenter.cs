using System.Globalization;
using Avalonia.Controls;
using FluentResults;

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

    public static string FormatErrors(IEnumerable<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(static error => error.Message));

    internal static string FormatMetrics(double speedMultiplier, double renderedFramesPerSecond) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{speedMultiplier:0.#}x | {renderedFramesPerSecond:0} fps"
        );

    internal static string FormatRomFileName(string romFileName) =>
        romFileName.Length <= RomFileNameMaxLength
            ? romFileName
            : $"{romFileName.AsSpan(0, RomFileNameMaxLength - 3)}...";
}
