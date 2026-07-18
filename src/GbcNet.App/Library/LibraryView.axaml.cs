// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using GbcNet.App.Shell.Chrome;

namespace GbcNet.App.Library;

internal sealed partial class LibraryView : UserControl
{
    private const int TileTitleMaxLength = 34;
    private const double CompactHeaderWidth = 760;

    private readonly List<Bitmap> _coverBitmaps = [];
    private LibraryHardwareFilter _hardwareFilter;
    private LibraryCoverFilter _coverFilter;
    private LibrarySortMode _sortMode;

    public LibraryView()
    {
        InitializeComponent();
        _hardwareFilter = LibraryHardwareFilter.All;
        _coverFilter = LibraryCoverFilter.All;
        _sortMode = LibrarySortMode.LastOpened;
        DetachedFromVisualTree += (_, _) => ClearTiles();
        LibraryFilterGrid.SizeChanged += (_, _) => UpdateHeaderLayout();
        LibrarySearchTextBox.TextChanged += (_, _) => QueryChanged?.Invoke();
    }

    public Action<LibraryEntry>? RomSelected { get; set; }
    public Action<LibraryEntry>? SetCoverRequested { get; set; }
    public Action<LibraryEntry>? ClearCoverRequested { get; set; }
    public Action<LibraryEntry>? RemoveRequested { get; set; }
    public Action? QueryChanged { get; set; }

    public LibraryQuery Query =>
        new(LibrarySearchTextBox.Text, _hardwareFilter, _coverFilter, _sortMode);

    public void Load(IReadOnlyList<LibraryEntry> entries)
    {
        ClearTiles();
        LibraryScrollViewer.IsVisible = entries.Count > 0;
        EmptyState.IsVisible = entries.Count == 0;
        EmptyStateText.Text = HasActiveQuery ? "No ROMs match" : "No ROMs yet";
        EmptyStateText.Foreground = AppChrome.Brush(AppChrome.Text);

        if (entries.Count == 0)
        {
            return;
        }

        var tiles = new List<LibraryTile>(entries.Count);
        foreach (var entry in entries)
        {
            tiles.Add(CreateTile(entry));
        }

        RomTilesControl.ItemsSource = tiles;
    }

    public Task<bool> ConfirmRemoveAsync()
    {
        var owner =
            TopLevel.GetTopLevel(this) as Window
            ?? throw new InvalidOperationException("Library view is not attached to a window.");

        return new DestructiveConfirmationWindow(
            title: "Remove ROM",
            heading: "Remove this ROM from your library?",
            message: "It will be removed from your GBC.Net library. The file stays on disk.",
            destructiveButtonLabel: "Remove"
        ).ShowDialog<bool>(owner);
    }

    public void ShowError(string message)
    {
        ClearTiles();
        LibraryScrollViewer.IsVisible = false;
        EmptyState.IsVisible = true;
        EmptyStateText.Text = message;
        EmptyStateText.Foreground = AppChrome.Brush(AppChrome.Error);
    }

    private bool HasActiveQuery =>
        !string.IsNullOrWhiteSpace(LibrarySearchTextBox.Text)
        || _hardwareFilter != LibraryHardwareFilter.All
        || _coverFilter != LibraryCoverFilter.All
        || _sortMode != LibrarySortMode.LastOpened;

    private void UpdateHeaderLayout()
    {
        if (LibraryFilterGrid.Bounds.Width <= 0)
        {
            return;
        }
        var compact = LibraryFilterGrid.Bounds.Width < CompactHeaderWidth;
        Grid.SetColumnSpan(LibrarySearchBorder, compact ? 2 : 1);
        Grid.SetRow(LibraryControlsPanel, compact ? 1 : 0);
        Grid.SetColumn(LibraryControlsPanel, compact ? 0 : 1);
        Grid.SetColumnSpan(LibraryControlsPanel, compact ? 2 : 1);
        LibrarySearchBorder.Margin = compact
            ? new Thickness(0)
            : new Thickness(left: 0, top: 0, right: 10, bottom: 0);
        LibraryControlsPanel.Margin = compact
            ? new Thickness(left: 0, top: 10, right: 0, bottom: 0)
            : new Thickness(0);
    }

    private void OnHardwareFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (
            sender is ComboBox comboBox
            && TryGetSelectedTag(comboBox, out LibraryHardwareFilter value)
            && _hardwareFilter != value
        )
        {
            _hardwareFilter = value;
            QueryChanged?.Invoke();
        }
    }

    private void OnCoverFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (
            sender is ComboBox comboBox
            && TryGetSelectedTag(comboBox, out LibraryCoverFilter value)
            && _coverFilter != value
        )
        {
            _coverFilter = value;
            QueryChanged?.Invoke();
        }
    }

    private void OnSortModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (
            sender is ComboBox comboBox
            && TryGetSelectedTag(comboBox, out LibrarySortMode value)
            && _sortMode != value
        )
        {
            _sortMode = value;
            QueryChanged?.Invoke();
        }
    }

    private static bool TryGetSelectedTag<T>(ComboBox comboBox, out T value)
        where T : struct, Enum
    {
        if (
            comboBox.SelectedItem is ComboBoxItem { Tag: string tag }
            && Enum.TryParse(tag, out T parsed)
        )
        {
            value = parsed;
            return true;
        }

        value = default;
        return false;
    }

    private LibraryTile CreateTile(LibraryEntry entry)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = TryLoadCoverBitmap(entry.CoverPath);
            var tile = new LibraryTile(entry, bitmap);
            if (bitmap is not null)
            {
                _coverBitmaps.Add(bitmap);
                bitmap = null;
            }

            return tile;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private void OnRomTileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTile tile })
        {
            RomSelected?.Invoke(tile.Entry);
        }
    }

    private void OnTileActionsButtonLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not LibraryTile tile)
        {
            return;
        }

        button.Flyout = CreateTileActionsFlyout(tile.Entry);
    }

    private static void OnTileActionsButtonClick(object? sender, RoutedEventArgs e) =>
        e.Handled = true;

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

    private void ClearTiles()
    {
        RomTilesControl.ItemsSource = null;
        DisposeCoverBitmaps();
    }

    private void DisposeCoverBitmaps()
    {
        foreach (var bitmap in _coverBitmaps)
        {
            bitmap.Dispose();
        }

        _coverBitmaps.Clear();
    }

    private sealed class LibraryTile(LibraryEntry entry, Bitmap? coverBitmap)
    {
        public LibraryEntry Entry { get; } = entry;
        public Bitmap? CoverBitmap { get; } = coverBitmap;
        public string Title { get; } =
            FormatTileTitle(Path.GetFileNameWithoutExtension(entry.FileName));
        public string PlayCountText { get; } =
            string.Create(
                provider: CultureInfo.InvariantCulture,
                handler: $"{entry.LaunchCount} play{(entry.LaunchCount == 1 ? string.Empty : "s")}"
            );
        public string HardwareText { get; } = entry.HardwareKind.ToString();
    }

    private static string FormatTileTitle(string fileName) =>
        fileName.Length <= TileTitleMaxLength
            ? fileName
            : $"{fileName.AsSpan(start: 0, length: TileTitleMaxLength - 3)}...";
}
