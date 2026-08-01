// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Shell.Chrome;

internal sealed class StatusBarPresenter(
    TextBlock message,
    Border coverFrame,
    Image coverImage,
    Border hardwareBadge,
    TextBlock hardwareBadgeText,
    Border speedBadge,
    TextBlock speed
) : IDisposable
{
    private Bitmap? _coverBitmap;

    public void ShowStatus(string text)
    {
        ClearCover();
        hardwareBadge.IsVisible = false;
        message.Foreground = AppChrome.Brush(AppChrome.Status);
        message.Text = text;
    }

    public void ShowError(string text)
    {
        ClearCover();
        hardwareBadge.IsVisible = false;
        message.Foreground = AppChrome.Brush(AppChrome.Error);
        message.Text = text;
    }

    public void ShowRomFileName(
        string romFileName,
        HardwareModel? hardwareModel = null,
        string? coverPath = null
    )
    {
        ShowStatus(FormatRomFileName(romFileName));
        if (hardwareModel is { } model)
        {
            hardwareBadgeText.Text = FormatHardwareModel(model);
            hardwareBadge.IsVisible = true;
        }

        if (coverPath is not null)
        {
            ShowCover(coverPath);
        }
    }

    public void ShowSpeed(string text)
    {
        speed.Text = text;
        speedBadge.IsVisible = !string.IsNullOrEmpty(text);
    }

    public void Dispose() => ClearCover();

    internal static string FormatRomFileName(string romFileName) =>
        Path.GetFileNameWithoutExtension(romFileName);

    internal static string FormatHardwareModel(HardwareModel hardwareModel) =>
        hardwareModel.ToString().ToUpperInvariant();

    private void ShowCover(string? coverPath)
    {
        ClearCover();
        if (coverPath is null || !File.Exists(coverPath))
        {
            return;
        }

        try
        {
            _coverBitmap = new Bitmap(coverPath);
            coverImage.Source = _coverBitmap;
            coverFrame.IsVisible = true;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or NotSupportedException
                        or ArgumentException
            )
        {
            ClearCover();
        }
    }

    private void ClearCover()
    {
        coverFrame.IsVisible = false;
        coverImage.Source = null;
        _coverBitmap?.Dispose();
        _coverBitmap = null;
    }
}
