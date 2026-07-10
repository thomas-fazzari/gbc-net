// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Configuration;

internal sealed partial class SettingsWindow : Window
{
    private static readonly FilePickerFileType _binaryFileType = new("Binary files")
    {
        Patterns = ["*.bin", "*"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/octet-stream"],
    };

    public SettingsWindow(BootRomConfig bootRomConfig)
    {
        InitializeComponent();

        DmgBootRomPathTextBox.Text = bootRomConfig.DmgPath;
        CgbBootRomPathTextBox.Text = bootRomConfig.CgbPath;
        SgbBootRomPathTextBox.Text = bootRomConfig.SgbPath;
    }

    private BootRomConfig GetBootRomConfig() =>
        new(
            NormalizePath(DmgBootRomPathTextBox.Text),
            NormalizePath(CgbBootRomPathTextBox.Text),
            NormalizePath(SgbBootRomPathTextBox.Text)
        );

    private static string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path;

    private async void BrowseDmgBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
                DmgBootRomPathTextBox,
                $"Select {BootRomConfig.DisplayName(HardwareModel.Dmg)} boot ROM"
            )
            .ConfigureAwait(true);

    private async void BrowseCgbBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
                CgbBootRomPathTextBox,
                $"Select {BootRomConfig.DisplayName(HardwareModel.Cgb)} boot ROM"
            )
            .ConfigureAwait(true);

    private async void BrowseSgbBootRomPathAsync(object? sender, RoutedEventArgs e) =>
        await BrowseBootRomAsync(
                SgbBootRomPathTextBox,
                $"Select {BootRomConfig.DisplayName(HardwareModel.Sgb)} boot ROM"
            )
            .ConfigureAwait(true);

    private void ClearDmgBootRomPath(object? sender, RoutedEventArgs e) =>
        DmgBootRomPathTextBox.Text = string.Empty;

    private void ClearCgbBootRomPath(object? sender, RoutedEventArgs e) =>
        CgbBootRomPathTextBox.Text = string.Empty;

    private void ClearSgbBootRomPath(object? sender, RoutedEventArgs e) =>
        SgbBootRomPathTextBox.Text = string.Empty;

    private void CancelSettings(object? sender, RoutedEventArgs e) => Close(null);

    private void SaveSettings(object? sender, RoutedEventArgs e) => Close(GetBootRomConfig());

    private async Task BrowseBootRomAsync(TextBox pathBox, string title)
    {
        var files = await StorageProvider
            .OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = [_binaryFileType],
                }
            )
            .ConfigureAwait(true);

        if (files.Count == 0)
        {
            return;
        }

        pathBox.Text = files[0].Path.IsFile ? files[0].Path.LocalPath : files[0].Path.ToString();
    }
}
