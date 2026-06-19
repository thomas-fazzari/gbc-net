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

    public SettingsWindow(BootRomPathOptions bootRomPaths)
    {
        InitializeComponent();

        _dmgBootRomPathTextBox = CreatePathBox(bootRomPaths.DmgPath);
        _cgbBootRomPathTextBox = CreatePathBox(bootRomPaths.CgbPath);

        Title = "Configuration";
        Background = SettingsChrome.Brush(SettingsChrome.Bg);
        Content = BuildContent();
    }

    private Grid BuildContent()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("152,*") };

        var sidebar = new Border
        {
            Background = SettingsChrome.Brush(SettingsChrome.Panel),
            BorderBrush = SettingsChrome.Brush(SettingsChrome.Hair),
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
            BootRomOptionsSchema.DmgNodeName.ToUpperInvariant(),
            _dmgBootRomPathTextBox,
            $"Select {BootRomOptionsSchema.DmgNodeName.ToUpperInvariant()} boot ROM"
        );
        AddPathRow(
            form,
            row: 1,
            BootRomOptionsSchema.CgbNodeName.ToUpperInvariant(),
            _cgbBootRomPathTextBox,
            $"Select {BootRomOptionsSchema.CgbNodeName.ToUpperInvariant()} boot ROM"
        );

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        var cancelButton = CreateButton("Cancel");
        cancelButton.Click += (_, _) => Close(null);
        var saveButton = CreateButton("Save", accent: true);
        saveButton.Click += (_, _) => Close(GetBootRomPathOptions());
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
        var label = CreateText(
            text,
            isSelected ? SettingsChrome.Text : SettingsChrome.Muted,
            fontSize: 14,
            FontWeight.SemiBold
        );
        Grid.SetColumn(label, 1);

        return new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(SettingsChrome.Radius),
            Background = isSelected
                ? SettingsChrome.Brush(SettingsChrome.SelectedSurface)
                : SettingsChrome.Brush(Colors.Transparent),
            BorderBrush = isSelected
                ? SettingsChrome.Brush(SettingsChrome.SelectedBorder)
                : SettingsChrome.Brush(Colors.Transparent),
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
            Foreground = SettingsChrome.Brush(SettingsChrome.Text),
            Background = SettingsChrome.Brush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusAdorner = null,
        };

    private void AddPathRow(Grid form, int row, string label, TextBox pathBox, string pickerTitle)
    {
        var labelBlock = CreateText(label, SettingsChrome.Text, fontSize: 13, FontWeight.SemiBold);
        form.Children.Add(labelBlock);
        Grid.SetRow(labelBlock, row);

        var pathWell = CreatePathWell(pathBox);
        form.Children.Add(pathWell);
        Grid.SetRow(pathWell, row);
        Grid.SetColumn(pathWell, 1);

        var browseButton = CreateButton("Browse");
        browseButton.Click += async (_, _) =>
            await BrowseBootRomAsync(pathBox, pickerTitle).ConfigureAwait(true);
        form.Children.Add(browseButton);
        Grid.SetRow(browseButton, row);
        Grid.SetColumn(browseButton, 2);

        var clearButton = CreateButton("Clear");
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
            Background = SettingsChrome.Brush(SettingsChrome.Surface),
            BorderBrush = SettingsChrome.Brush(SettingsChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(SettingsChrome.Radius),
            Child = pathBox,
        };
        pathBox.GotFocus += (_, _) =>
            well.BorderBrush = SettingsChrome.Brush(SettingsChrome.Strong);
        pathBox.LostFocus += (_, _) => well.BorderBrush = SettingsChrome.Brush(SettingsChrome.Hair);
        return well;
    }

    private static Button CreateButton(string text, bool accent = false)
    {
        var button = new Button
        {
            Content = text,
            Height = 30,
            MinWidth = 0,
            Padding = new Thickness(10, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = SettingsChrome.Brush(
                accent ? SettingsChrome.AccentOn : SettingsChrome.Text
            ),
            Background = SettingsChrome.Brush(
                accent ? SettingsChrome.Accent : SettingsChrome.Raise
            ),
            BorderBrush = SettingsChrome.Brush(
                accent ? SettingsChrome.Accent : SettingsChrome.Hair
            ),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(SettingsChrome.Radius),
        };
        SettingsChrome.AttachHover(button, accent);
        return button;
    }

    private static TextBlock CreateText(
        string text,
        Color color,
        double fontSize,
        FontWeight fontWeight
    ) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = SettingsChrome.Brush(color),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private BootRomPathOptions GetBootRomPathOptions() =>
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

file static class SettingsChrome
{
    public const double Radius = 4;

    public static readonly Color Bg = Color.Parse("#18181b");
    public static readonly Color Panel = Color.Parse("#202024");
    public static readonly Color Surface = Color.Parse("#26262b");
    public static readonly Color Raise = Color.Parse("#2f2f35");
    public static readonly Color Hover = Color.Parse("#38383f");
    public static readonly Color Press = Color.Parse("#44444c");
    public static readonly Color Hair = Color.FromArgb(28, 255, 255, 255);
    public static readonly Color Text = Color.Parse("#f2f2f3");
    public static readonly Color Muted = Color.Parse("#8f8f99");
    public static readonly Color Strong = Color.FromArgb(56, 255, 255, 255);
    public static readonly Color SelectedSurface = Color.Parse("#303036");
    public static readonly Color SelectedBorder = Color.FromArgb(42, 255, 255, 255);
    public static readonly Color Accent = Color.Parse("#3a3a41");
    public static readonly Color AccentOn = Text;

    private static readonly Color FocusBlue = Color.Parse("#0078d4");
    private static readonly Color FocusBluePressed = Color.Parse("#005a9e");

    public static SolidColorBrush Brush(Color color) => new(color);

    public static void AttachHover(Button button, bool accent)
    {
        var normal = accent ? Accent : Raise;
        var hover = accent ? FocusBlue : Hover;
        var press = accent ? FocusBluePressed : Press;
        button.PointerEntered += (_, _) => button.Background = Brush(hover);
        button.PointerExited += (_, _) => button.Background = Brush(normal);
        button.PointerPressed += (_, _) => button.Background = Brush(press);
        button.PointerReleased += (_, e) =>
        {
            button.Background = new Rect(button.Bounds.Size).Contains(e.GetPosition(button))
                ? Brush(hover)
                : Brush(normal);
        };
    }
}
