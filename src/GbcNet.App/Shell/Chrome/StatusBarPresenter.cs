using Avalonia.Controls;

namespace GbcNet.App.Shell.Chrome;

internal sealed class StatusBarPresenter(TextBlock message, TextBlock speed)
{
    private const int RomFileNameMaxLength = 72;

    public void ShowStatus(string text)
    {
        message.Foreground = AppChrome.Brush(AppChrome.Status);
        message.Text = text;
    }

    public void ShowError(string text)
    {
        message.Foreground = AppChrome.Brush(AppChrome.Error);
        message.Text = text;
    }

    public void ShowRomFileName(string romFileName)
    {
        ShowStatus(FormatRomFileName(romFileName));
    }

    public void ShowSpeed(string text)
    {
        speed.Text = text;
    }

    internal static string FormatRomFileName(string romFileName) =>
        romFileName.Length <= RomFileNameMaxLength
            ? romFileName
            : $"{romFileName.AsSpan(0, RomFileNameMaxLength - 3)}...";
}
