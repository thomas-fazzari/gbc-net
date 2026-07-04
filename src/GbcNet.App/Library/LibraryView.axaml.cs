// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GbcNet.App.Library.Entities;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Cartridges;

namespace GbcNet.App.Library;

internal sealed partial class LibraryView : UserControl
{
    private const int TileTitleMaxLength = 34;

    public LibraryView()
    {
        InitializeComponent();
    }

    public Action<LibraryEntry>? RomSelected { get; set; }

    public void Load(IReadOnlyList<LibraryEntry> entries)
    {
        RomTilesPanel.Children.Clear();
        LibraryScrollViewer.IsVisible = entries.Count > 0;
        EmptyState.IsVisible = entries.Count == 0;
        EmptyStateText.Text = "No ROMs yet";
        EmptyStateText.Foreground = AppChrome.Brush(AppChrome.Text);

        foreach (var entry in entries)
        {
            RomTilesPanel.Children.Add(CreateRomTile(entry));
        }
    }

    public void ShowError(string message)
    {
        RomTilesPanel.Children.Clear();
        LibraryScrollViewer.IsVisible = false;
        EmptyState.IsVisible = true;
        EmptyStateText.Text = message;
        EmptyStateText.Foreground = AppChrome.Brush(AppChrome.Error);
    }

    private Button CreateRomTile(LibraryEntry entry)
    {
        var button = new Button
        {
            Width = 168,
            Height = 214,
            Margin = new Thickness(0, 0, 18, 18),
            Padding = new Thickness(10),
            Background = AppChrome.Brush(AppChrome.Panel),
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(AppChrome.Radius),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = CreateRomTileContent(entry),
        };
        button.Click += (_, _) => RomSelected?.Invoke(entry);
        return button;
    }

    private static Grid CreateRomTileContent(LibraryEntry entry) =>
        new()
        {
            RowDefinitions = new RowDefinitions("124,52,18"),
            Children = { CreateCoverPlaceholder(), CreateTitle(entry), CreateFooter(entry) },
        };

    private static Border CreateCoverPlaceholder() =>
        new()
        {
            Background = AppChrome.Brush(AppChrome.Surface),
            CornerRadius = new CornerRadius(AppChrome.Radius),
            Child = new TextBlock
            {
                Text = "ROM",
                Foreground = AppChrome.Brush(AppChrome.Muted),
                FontSize = 26,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

    private static Border CreateHardwareKindBadge(CartridgeHardwareKind hardwareKind) =>
        new()
        {
            Background = Brushes.Transparent,
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(5, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = hardwareKind.ToString(),
                Foreground = AppChrome.Brush(AppChrome.Muted),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
            },
        };

    private static TextBlock CreateTitle(LibraryEntry entry)
    {
        var title = new TextBlock
        {
            Text = FormatTileTitle(Path.GetFileNameWithoutExtension(entry.FileName)),
            Foreground = AppChrome.Brush(AppChrome.Text),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 10, 0, 0),
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetRow(title, 1);
        return title;
    }

    private static Grid CreateFooter(LibraryEntry entry)
    {
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0),
        };
        footer.Children.Add(
            new TextBlock
            {
                Text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.LaunchCount} play{(entry.LaunchCount == 1 ? string.Empty : "s")}"
                ),
                Foreground = AppChrome.Brush(AppChrome.Muted),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            }
        );
        var badge = CreateHardwareKindBadge(entry.HardwareKind);
        Grid.SetColumn(badge, 1);
        footer.Children.Add(badge);
        Grid.SetRow(footer, 2);
        return footer;
    }

    private static string FormatTileTitle(string fileName) =>
        fileName.Length <= TileTitleMaxLength
            ? fileName
            : $"{fileName.AsSpan(0, TileTitleMaxLength - 3)}...";
}
