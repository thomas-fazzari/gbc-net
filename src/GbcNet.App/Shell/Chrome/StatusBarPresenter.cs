using Avalonia.Controls;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Shell.Chrome;

internal sealed class StatusBarPresenter(
    TextBlock message,
    Border hardwareBadge,
    TextBlock hardwareBadgeText,
    Border speedBadge,
    TextBlock speed
)
{
    private const int RomFileNameMaxLength = 72;

    public void ShowStatus(string text)
    {
        hardwareBadge.IsVisible = false;
        message.Foreground = AppChrome.Brush(AppChrome.Status);
        message.Text = text;
    }

    public void ShowError(string text)
    {
        hardwareBadge.IsVisible = false;
        message.Foreground = AppChrome.Brush(AppChrome.Error);
        message.Text = text;
    }

    public void ShowRomFileName(string romFileName)
    {
        ShowStatus(FormatRomFileName(romFileName));
    }

    public void ShowRomFileName(string romFileName, HardwareModel hardwareModel)
    {
        ShowStatus(FormatRomFileName(romFileName));
        hardwareBadgeText.Text = FormatHardwareModel(hardwareModel);
        hardwareBadge.IsVisible = true;
    }

    public void ShowSpeed(string text)
    {
        speed.Text = text;
        speedBadge.IsVisible = !string.IsNullOrEmpty(text);
    }

    internal static string FormatRomFileName(string romFileName) =>
        FormatStatusText(Path.GetFileNameWithoutExtension(romFileName));

    internal static string FormatHardwareModel(HardwareModel hardwareModel) =>
        hardwareModel.ToString().ToUpperInvariant();

    private static string FormatStatusText(string text) =>
        text.Length <= RomFileNameMaxLength
            ? text
            : $"{text.AsSpan(0, RomFileNameMaxLength - 3)}...";
}
