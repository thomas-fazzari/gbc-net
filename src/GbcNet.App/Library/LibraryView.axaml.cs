// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GbcNet.App.Library.Entities;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Cartridges;

namespace GbcNet.App.Library;

internal sealed partial class LibraryView : UserControl
{
    private const int TileTitleMaxLength = 34;
    private readonly List<Bitmap> _coverBitmaps = [];

    public LibraryView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => DisposeCoverBitmaps();
    }

    public Action<LibraryEntry>? RomSelected { get; set; }
    public Action<LibraryEntry>? SetCoverRequested { get; set; }
    public Action<LibraryEntry>? ClearCoverRequested { get; set; }
    public Action<LibraryEntry>? RemoveRequested { get; set; }

    public void Load(IReadOnlyList<LibraryEntry> entries)
    {
        RomTilesPanel.Children.Clear();
        DisposeCoverBitmaps();
        LibraryScrollViewer.IsVisible = entries.Count > 0;
        EmptyState.IsVisible = entries.Count == 0;
        EmptyStateText.Text = "No ROMs yet";
        EmptyStateText.Foreground = AppChrome.Brush(AppChrome.Text);

        foreach (var entry in entries)
        {
            RomTilesPanel.Children.Add(CreateRomTile(entry));
        }
    }

    public Task<bool> ConfirmRemoveAsync()
    {
        var owner =
            TopLevel.GetTopLevel(this) as Window
            ?? throw new InvalidOperationException("Library view is not attached to a window.");
        return new RemoveLibraryEntryWindow().ShowDialog<bool>(owner);
    }

    public void ShowError(string message)
    {
        RomTilesPanel.Children.Clear();
        DisposeCoverBitmaps();
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
            Height = 238,
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

    private Grid CreateRomTileContent(LibraryEntry entry) =>
        new()
        {
            RowDefinitions = new RowDefinitions("148,52,18"),
            Children = { CreateCover(entry), CreateTitle(entry), CreateFooter(entry) },
        };

    private Border CreateCover(LibraryEntry entry)
    {
        var cover = CreateCoverPlaceholder();
        Bitmap? bitmap = null;
        try
        {
            bitmap = TryLoadCoverBitmap(entry.CoverPath);
            if (bitmap is null)
            {
                return cover;
            }

            _coverBitmaps.Add(bitmap);
            cover.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
            bitmap = null;
            return cover;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private static Border CreateCoverPlaceholder() =>
        new()
        {
            Background = AppChrome.Brush(AppChrome.Surface),
            ClipToBounds = true,
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

    private Grid CreateFooter(LibraryEntry entry)
    {
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
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
        var actions = CreateTileActionsButton(entry);
        Grid.SetColumn(actions, 2);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        return footer;
    }

    private Button CreateTileActionsButton(LibraryEntry entry)
    {
        var button = new Button
        {
            Content = "⋯",
            MinWidth = 26,
            Height = 18,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Foreground = AppChrome.Brush(AppChrome.Muted),
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Flyout = CreateTileActionsFlyout(entry),
        };
        button.Click += (_, e) => e.Handled = true;
        return button;
    }

    private MenuFlyout CreateTileActionsFlyout(LibraryEntry entry)
    {
        var setCover = new MenuItem { Header = "Set Cover..." };
        setCover.Click += (_, _) => SetCoverRequested?.Invoke(entry);

        var items = new List<MenuItem> { setCover };
        if (entry.CoverPath is not null)
        {
            var clearCover = new MenuItem { Header = "Clear Cover" };
            clearCover.Click += (_, _) => ClearCoverRequested?.Invoke(entry);
            items.Add(clearCover);
        }

        var remove = new MenuItem { Header = "Remove from Library..." };
        remove.Click += (_, _) => RemoveRequested?.Invoke(entry);
        items.Add(remove);

        return new MenuFlyout { ItemsSource = items };
    }

    private static Bitmap? TryLoadCoverBitmap(string? coverPath)
    {
        if (coverPath is null || !File.Exists(coverPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(coverPath);
            return new Bitmap(stream);
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
            return null;
        }
    }

    private void DisposeCoverBitmaps()
    {
        foreach (var bitmap in _coverBitmaps)
        {
            bitmap.Dispose();
        }

        _coverBitmaps.Clear();
    }

    private sealed class RemoveLibraryEntryWindow : Window
    {
        public RemoveLibraryEntryWindow()
        {
            Title = "Remove ROM";
            SizeToContent = SizeToContent.WidthAndHeight;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppChrome.Brush(AppChrome.Bg);
            Content = BuildContent();
        }

        private StackPanel BuildContent()
        {
            var cancelButton = AppChrome.Button("Cancel");
            cancelButton.Click += (_, _) => Close(dialogResult: false);

            var removeButton = AppChrome.Button("Remove", accent: true);
            removeButton.Click += (_, _) => Close(dialogResult: true);

            return new StackPanel
            {
                Width = 360,
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Remove this ROM from your library?",
                        Foreground = AppChrome.Brush(AppChrome.Text),
                        FontSize = 16,
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text =
                            "It will be removed from your GBC.Net library. The file stays on disk.",
                        Foreground = AppChrome.Brush(AppChrome.Muted),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, removeButton },
                    },
                },
            };
        }
    }

    private static string FormatTileTitle(string fileName) =>
        fileName.Length <= TileTitleMaxLength
            ? fileName
            : $"{fileName.AsSpan(0, TileTitleMaxLength - 3)}...";
}
