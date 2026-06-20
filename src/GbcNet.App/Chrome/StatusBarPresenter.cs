using System.Globalization;
using Avalonia.Controls;
using FluentResults;

namespace GbcNet.App.Chrome;

internal sealed class StatusBarPresenter(TextBlock message, TextBlock metrics)
{
    private const int RomNameMaxLength = 72;

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

    public void ShowRom(string romName)
    {
        ShowStatus(FormatRomName(romName));
    }

    public void ShowMetrics(double targetSpeed, double displayFramesPerSecond)
    {
        metrics.Text = FormatMetrics(targetSpeed, displayFramesPerSecond);
    }

    public static string FormatErrors(IEnumerable<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(static error => error.Message));

    internal static string FormatMetrics(double targetSpeed, double displayFramesPerSecond) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{targetSpeed:0.#}x | {displayFramesPerSecond:0} fps"
        );

    internal static string FormatRomName(string romName) =>
        romName.Length <= RomNameMaxLength
            ? romName
            : $"{romName.AsSpan(0, RomNameMaxLength - 3)}...";
}
