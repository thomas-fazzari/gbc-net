// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.App.Shell.Chrome;
using GbcNet.App.Sorting;

namespace GbcNet.App.Library;

internal sealed partial class LibraryView : UserControl, INotifyPropertyChanged
{
    private readonly List<Bitmap> _coverBitmaps = [];
    private LibraryHardwareFilter _hardwareFilter;
    private LibraryRegionFilter _regionFilter;
    private LibrarySortField? _tableSortMode;
    private SortDirection? _tableSortDirection;
    private LibraryViewMode _viewMode;
    private bool _refreshingFilters;
    private PropertyChangedEventHandler? _propertyChanged;

    public LibraryView()
    {
        InitializeComponent();

        _hardwareFilter = LibraryHardwareFilter.All;
        _regionFilter = LibraryRegionFilter.All;
        SetViewMode(LibraryViewMode.Grid);
        DetachedFromVisualTree += (_, _) => ClearTiles();
    }

    public Action<LibraryEntry>? RomSelected { get; set; }
    public Action<LibraryEntry>? SetCoverRequested { get; set; }
    public Action<LibraryEntry>? ClearCoverRequested { get; set; }
    public Action<LibraryEntry>? RemoveRequested { get; set; }
    public Action? OpenRomRequested { get; set; }
    public Action? QueryChanged { get; set; }
    public Action<LibraryViewMode>? ViewModeChanged { get; set; }

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    public string SearchText
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
            NotifyQueryChanged();
        }
    } = string.Empty;

    public bool IsTitleSortAscending =>
        IsSortIndicatorVisible(LibrarySortField.Title, SortDirection.Ascending);
    public bool IsTitleSortDescending =>
        IsSortIndicatorVisible(LibrarySortField.Title, SortDirection.Descending);
    public bool IsTimePlayedSortAscending =>
        IsSortIndicatorVisible(LibrarySortField.MostTimePlayed, SortDirection.Ascending);
    public bool IsTimePlayedSortDescending =>
        IsSortIndicatorVisible(LibrarySortField.MostTimePlayed, SortDirection.Descending);
    public bool IsPlaysSortAscending =>
        IsSortIndicatorVisible(LibrarySortField.MostPlayed, SortDirection.Ascending);
    public bool IsPlaysSortDescending =>
        IsSortIndicatorVisible(LibrarySortField.MostPlayed, SortDirection.Descending);
    public bool IsLastPlayedSortAscending =>
        IsSortIndicatorVisible(LibrarySortField.LastOpened, SortDirection.Ascending);
    public bool IsLastPlayedSortDescending =>
        IsSortIndicatorVisible(LibrarySortField.LastOpened, SortDirection.Descending);

    public LibraryQuery Query =>
        new(
            SearchText: SearchText,
            Hardware: _hardwareFilter,
            Sort: _tableSortMode ?? LibrarySortField.LastOpened,
            Region: _regionFilter,
            Direction: _tableSortDirection
        );

    public void SetViewMode(LibraryViewMode viewMode)
    {
        if (!Enum.IsDefined(viewMode))
        {
            throw new ArgumentOutOfRangeException(nameof(viewMode));
        }

        _viewMode = viewMode;
        RomGridControl.IsVisible = viewMode is LibraryViewMode.Grid;
        LibraryScrollViewer.IsVisible =
            viewMode is LibraryViewMode.Grid && RomGridControl.ItemsSource is not null;
        RomTableView.IsVisible =
            viewMode is LibraryViewMode.List && RomTableView.ItemsSource is not null;
        GridViewToggle.IsChecked = viewMode is LibraryViewMode.Grid;
        ListViewToggle.IsChecked = viewMode is LibraryViewMode.List;
    }

    public void Load(IReadOnlyList<LibraryEntry> entries)
    {
        ClearTiles();
        var isEmpty = entries.Count == 0;
        var hasActiveQuery = HasActiveQuery;
        LibraryScrollViewer.IsVisible = !isEmpty && _viewMode is LibraryViewMode.Grid;
        RomTableView.IsVisible = !isEmpty && _viewMode is LibraryViewMode.List;
        EmptyState.IsVisible = isEmpty;
        EmptyStateText.Text = hasActiveQuery ? "No matching ROMs" : "No ROMs yet";
        EmptyStateDescription.IsVisible = true;
        EmptyStateDescription.Text = hasActiveQuery
            ? "Try changing or clearing your filters."
            : "Open a ROM to add it to your library.";
        ClearFiltersButton.IsVisible = hasActiveQuery;
        OpenRomButton.IsVisible = !hasActiveQuery;

        if (isEmpty)
        {
            return;
        }

        var tiles = new List<LibraryTile>(entries.Count);
        foreach (var entry in entries)
        {
            tiles.Add(CreateTile(entry));
        }

        RomGridControl.ItemsSource = tiles;
        RomTableView.ItemsSource = tiles;
    }

    public Task<bool> ConfirmRemoveAsync()
    {
        var owner =
            TopLevel.GetTopLevel(this) as Window
            ?? throw new InvalidOperationException("Library view is not attached to a window.");

        return new DestructiveConfirmationWindow(
            title: "Remove ROM",
            heading: "Remove this ROM from your library?",
            message: "This removes the ROM from your library, not from disk.",
            destructiveButtonLabel: "Remove"
        ).ShowDialog<bool>(owner);
    }

    private bool HasActiveQuery =>
        !string.IsNullOrWhiteSpace(SearchText)
        || _hardwareFilter != LibraryHardwareFilter.All
        || _regionFilter != LibraryRegionFilter.All
        || _tableSortMode is not null;

    private void OnOpenRomClick(object? sender, RoutedEventArgs e) => OpenRomRequested?.Invoke();

    private void OnViewModeChanged(object? sender, RoutedEventArgs e)
    {
        if (
            sender is ToggleButton { Tag: string tag }
            && Enum.TryParse(tag, out LibraryViewMode viewMode)
            && _viewMode != viewMode
        )
        {
            SetViewMode(viewMode);
            ViewModeChanged?.Invoke(viewMode);
        }
        else
        {
            SetViewMode(_viewMode);
        }
    }

    private void OnHardwareFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TryGetSelectedTag(sender, out LibraryHardwareFilter value) && _hardwareFilter != value)
        {
            _hardwareFilter = value;
            NotifyQueryChanged();
        }
    }

    private void OnRegionFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TryGetSelectedTag(sender, out LibraryRegionFilter value) && _regionFilter != value)
        {
            _regionFilter = value;
            NotifyQueryChanged();
        }
    }

    private void OnTableSortClick(object? sender, RoutedEventArgs e)
    {
        if (
            sender is not Button { Tag: string tag }
            || !Enum.TryParse(tag, out LibrarySortField value)
        )
        {
            return;
        }

        var defaultDirection = LibraryQuery.GetDefaultDirection(value);
        if (_tableSortMode != value)
        {
            _tableSortMode = value;
            _tableSortDirection = defaultDirection;
        }
        else if (_tableSortDirection == defaultDirection)
        {
            _tableSortDirection =
                defaultDirection is SortDirection.Ascending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;
        }
        else
        {
            _tableSortMode = null;
            _tableSortDirection = null;
        }

        UpdateSortIndicators();

        NotifyQueryChanged();
    }

    private void ClearFilters(object? sender, RoutedEventArgs e)
    {
        _refreshingFilters = true;
        try
        {
            SearchText = string.Empty;
            _hardwareFilter = LibraryHardwareFilter.All;
            _regionFilter = LibraryRegionFilter.All;
            _tableSortMode = null;
            _tableSortDirection = null;
            HardwareFilter.SelectedIndex = 0;
            RegionFilter.SelectedIndex = 0;
        }
        finally
        {
            _refreshingFilters = false;
        }

        UpdateSortIndicators();

        QueryChanged?.Invoke();
    }

    private void UpdateSortIndicators() =>
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    private bool IsSortIndicatorVisible(LibrarySortField sortMode, SortDirection sortDirection) =>
        _tableSortMode == sortMode && _tableSortDirection == sortDirection;

    private void NotifyQueryChanged()
    {
        if (!_refreshingFilters)
        {
            QueryChanged?.Invoke();
        }
    }

    private static bool TryGetSelectedTag<T>(object? sender, out T value)
        where T : struct, Enum
    {
        if (
            sender is ComboBox { SelectedItem: ComboBoxItem { Tag: string tag } }
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
            bitmap = ThumbnailUtils.TryLoad(entry.CoverPath);
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
        if (
            sender is TableView tableView
            && e is SelectionChangedEventArgs { AddedItems: [LibraryTile selectedTile] }
        )
        {
            tableView.SelectedItem = null;
            RomSelected?.Invoke(selectedTile.Entry);
            return;
        }

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

    private MenuFlyout CreateTileActionsFlyout(LibraryEntry entry)
    {
        var setCover = new MenuItem { Header = "Attach Cover..." };
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

    private void ClearTiles()
    {
        RomGridControl.ItemsSource = null;
        RomTableView.ItemsSource = null;
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
}

internal sealed class LibraryTile(LibraryEntry entry, Bitmap? coverBitmap)
{
    public LibraryEntry Entry { get; } = entry;
    public Bitmap? CoverBitmap { get; } = coverBitmap;

    public string UserFriendlyTitle { get; } =
        entry.NoIntroMetadata?.Title ?? Path.GetFileNameWithoutExtension(entry.FileName);

    public string CartridgeTitle { get; } = entry.CartridgeTitle ?? string.Empty;
    public bool HasCartridgeTitle { get; } = !string.IsNullOrWhiteSpace(entry.CartridgeTitle);

    public string PlayCountText { get; } = entry.LaunchCount.ToString(CultureInfo.InvariantCulture);

    public string PlayTimeText { get; } = FormatPlayTime(entry.PlayTime);

    public string HardwareText { get; } = entry.HardwareKind.ToString();

    public bool HasJapan { get; } =
        entry.NoIntroMetadata?.Regions.HasFlag(NoIntroRegion.Japan) is true;
    public bool HasUsa { get; } = entry.NoIntroMetadata?.Regions.HasFlag(NoIntroRegion.Usa) is true;
    public bool HasEurope { get; } =
        entry.NoIntroMetadata?.Regions.HasFlag(NoIntroRegion.Europe) is true;
    public bool HasWorld { get; } =
        entry.NoIntroMetadata?.Regions.HasFlag(NoIntroRegion.World) is true;
    public bool HasOther { get; } =
        entry.NoIntroMetadata is { Regions: var regions }
        && (
            regions
            & ~(
                NoIntroRegion.Japan | NoIntroRegion.Usa | NoIntroRegion.Europe | NoIntroRegion.World
            )
        ) != NoIntroRegion.None;

    public string LastPlayedText { get; } =
        entry.LastOpenedAt.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

    private static string FormatPlayTime(TimeSpan playTime)
    {
        if (playTime < TimeSpan.FromMinutes(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{playTime.Seconds} s");
        }

        if (playTime < TimeSpan.FromHours(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{playTime.Minutes} min");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)playTime.TotalHours}h {playTime.Minutes:D2}m"
        );
    }
}
