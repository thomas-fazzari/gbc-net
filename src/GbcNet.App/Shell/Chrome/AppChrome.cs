using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GbcNet.App.Shell.Chrome;

internal static class AppChrome
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
    public static readonly Color Status = Color.Parse("#a1a1aa");
    public static readonly Color Error = Color.Parse("#fca5a5");

    private static readonly Color FocusBlue = Color.Parse("#0078d4");
    private static readonly Color FocusBluePressed = Color.Parse("#005a9e");

    public static SolidColorBrush Brush(Color color) => new(color);

    public static TextBlock TextBlock(string text, Color color, double fontSize) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(color),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

    public static Button Button(string text, bool accent = false)
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
            Foreground = Brush(accent ? AccentOn : Text),
            Background = Brush(accent ? Accent : Raise),
            BorderBrush = Brush(accent ? Accent : Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Radius),
        };
        AttachHover(button, accent);
        return button;
    }

    private static void AttachHover(Button button, bool accent)
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
