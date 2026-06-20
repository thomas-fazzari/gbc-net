using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GbcNet.App.Chrome;
using GbcNet.App.Configuration.Sections.BootRom;

namespace GbcNet.App.Configuration;

internal sealed partial class SettingsWindow : Window
{
    private static readonly FilePickerFileType _binaryFileType = new("Binary files")
    {
        Patterns = ["*.bin", "*"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/octet-stream"],
    };

    private readonly TextBox _dmgBootRomPathTextBox;
    private readonly TextBox _cgbBootRomPathTextBox;

    public SettingsWindow(BootRomConfig bootRomConfig)
    {
        InitializeComponent();

        _dmgBootRomPathTextBox = CreatePathBox(bootRomConfig.DmgPath);
        _cgbBootRomPathTextBox = CreatePathBox(bootRomConfig.CgbPath);

        Title = "Configuration";
        Background = AppChrome.Brush(AppChrome.Bg);
        Content = BuildContent();
    }

    private Grid BuildContent()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("152,*") };

        var sidebar = new Border
        {
            Background = AppChrome.Brush(AppChrome.Panel),
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(8),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    CreateSidebarItem("Boot ROMs", Icons.FileCog, isSelected: true),
                    CreateSidebarItem("Inputs", Icons.Gamepad, isSelected: false),
                },
            },
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(32, 28, 32, 24),
        };

        var form = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("48,*,72,64"),
            RowSpacing = 12,
            ColumnSpacing = 8,
        };
        AddPathRow(
            form,
            row: 0,
            BootRomConfigSchema.DmgNodeName.ToUpperInvariant(),
            _dmgBootRomPathTextBox,
            $"Select {BootRomConfigSchema.DmgNodeName.ToUpperInvariant()} boot ROM"
        );
        AddPathRow(
            form,
            row: 1,
            BootRomConfigSchema.CgbNodeName.ToUpperInvariant(),
            _cgbBootRomPathTextBox,
            $"Select {BootRomConfigSchema.CgbNodeName.ToUpperInvariant()} boot ROM"
        );

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        var cancelButton = AppChrome.Button("Cancel");
        cancelButton.Click += (_, _) => Close(null);
        var saveButton = AppChrome.Button("Save", accent: true);
        saveButton.Click += (_, _) => Close(GetBootRomConfig());
        footer.Children.Add(cancelButton);
        footer.Children.Add(saveButton);

        root.Children.Add(sidebar);
        content.Children.Add(form);
        Grid.SetRow(footer, 2);
        content.Children.Add(footer);
        root.Children.Add(content);
        Grid.SetColumn(content, 1);
        return root;
    }

    private static Border CreateSidebarItem(string text, string iconAsset, bool isSelected)
    {
        var icon = Icons.Make(iconAsset, size: 15);
        var label = AppChrome.TextBlock(
            text,
            isSelected ? AppChrome.Text : AppChrome.Muted,
            fontSize: 14
        );
        Grid.SetColumn(label, 1);

        return new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(AppChrome.Radius),
            Background = isSelected
                ? AppChrome.Brush(AppChrome.SelectedSurface)
                : AppChrome.Brush(Colors.Transparent),
            BorderBrush = isSelected
                ? AppChrome.Brush(AppChrome.SelectedBorder)
                : AppChrome.Brush(Colors.Transparent),
            Padding = new Thickness(12, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("16,*"),
                ColumnSpacing = 9,
                Children = { icon, label },
            },
        };
    }

    private static TextBox CreatePathBox(string? text) =>
        new()
        {
            Text = text,
            PlaceholderText = "No file selected",
            Height = 30,
            FontSize = 12,
            Foreground = AppChrome.Brush(AppChrome.Text),
            Background = AppChrome.Brush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusAdorner = null,
        };

    private void AddPathRow(Grid form, int row, string label, TextBox pathBox, string pickerTitle)
    {
        var labelBlock = AppChrome.TextBlock(label, AppChrome.Text, fontSize: 13);
        form.Children.Add(labelBlock);
        Grid.SetRow(labelBlock, row);

        var pathWell = CreatePathWell(pathBox);
        form.Children.Add(pathWell);
        Grid.SetRow(pathWell, row);
        Grid.SetColumn(pathWell, 1);

        var browseButton = AppChrome.Button("Browse");
        browseButton.Click += async (_, _) =>
            await BrowseBootRomAsync(pathBox, pickerTitle).ConfigureAwait(true);
        form.Children.Add(browseButton);
        Grid.SetRow(browseButton, row);
        Grid.SetColumn(browseButton, 2);

        var clearButton = AppChrome.Button("Clear");
        clearButton.Click += (_, _) => pathBox.Text = string.Empty;
        form.Children.Add(clearButton);
        Grid.SetRow(clearButton, row);
        Grid.SetColumn(clearButton, 3);
    }

    private static Border CreatePathWell(TextBox pathBox)
    {
        var well = new Border
        {
            Height = 32,
            Background = AppChrome.Brush(AppChrome.Surface),
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(AppChrome.Radius),
            Child = pathBox,
        };
        pathBox.GotFocus += (_, _) => well.BorderBrush = AppChrome.Brush(AppChrome.Strong);
        pathBox.LostFocus += (_, _) => well.BorderBrush = AppChrome.Brush(AppChrome.Hair);
        return well;
    }

    private BootRomConfig GetBootRomConfig() =>
        new(NormalizePath(_dmgBootRomPathTextBox.Text), NormalizePath(_cgbBootRomPathTextBox.Text));

    private static string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path;

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
