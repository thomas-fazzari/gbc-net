using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.Gui.Emulation;

namespace GbcNet.Gui.Menus;

internal sealed partial class MainMenu : UserControl
{
    private static readonly KeyGesture _fastForwardGesture = KeyGesture.Parse("Tab");

    private readonly NativeMenuItem _nativePauseMenuItem = new("Pause")
    {
        Gesture = KeyGesture.Parse("Space"),
        IsEnabled = false,
    };

    private readonly NativeMenuItem _nativeResetMenuItem = new("Reset")
    {
        Gesture = KeyGesture.Parse("Meta+R"),
        IsEnabled = false,
    };

    private readonly NativeMenuItem _nativeFastForwardMenuItem = new("Fast Forward")
    {
        Gesture = _fastForwardGesture,
        ToggleType = MenuItemToggleType.CheckBox,
    };

    private readonly List<(
        NativeMenuItem Item,
        EmulationSpeed Speed
    )> _nativeFastForwardSpeedMenuItems = [];
    private readonly List<(MenuItem Item, EmulationSpeed Speed)> _windowFastForwardSpeedMenuItems =
    [];
    private readonly NativeMenu _nativeMenu;

    public MainMenu()
    {
        InitializeComponent();

        IsVisible = !OperatingSystem.IsMacOS();
        _nativeMenu = CreateNativeMenu();
        ConfigureWindowMenu();
    }

    public event EventHandler? OpenRomRequested;

    public event EventHandler? CloseRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? ResetRequested;

    public event EventHandler? FastForwardRequested;

    public event EventHandler<FastForwardSpeedSelectedEventArgs>? FastForwardSpeedSelected;

    public void AttachNativeMenu(Window window)
    {
        if (OperatingSystem.IsMacOS())
        {
            NativeMenu.SetMenu(window, _nativeMenu);
        }
    }

    public void SetEmulationActionsEnabled(bool isEnabled)
    {
        SetPauseState(isEnabled, isPaused: false);
        _nativeResetMenuItem.IsEnabled = isEnabled;
        ResetEmulationMenuItem.IsEnabled = isEnabled;
    }

    public void SetPauseState(bool isEnabled, bool isPaused)
    {
        string header = isPaused ? "Resume" : "Pause";

        _nativePauseMenuItem.Header = header;
        _nativePauseMenuItem.IsEnabled = isEnabled;

        PauseEmulationMenuItem.Header = header;
        PauseEmulationMenuItem.IsEnabled = isEnabled;
    }

    public void SetFastForwardState(bool isEnabled, EmulationSpeed speed)
    {
        _nativeFastForwardMenuItem.IsChecked = isEnabled;
        FastForwardMenuItem.IsChecked = isEnabled;

        foreach (
            (NativeMenuItem item, EmulationSpeed itemSpeed) in _nativeFastForwardSpeedMenuItems
        )
        {
            item.IsChecked = itemSpeed == speed;
        }

        foreach ((MenuItem item, EmulationSpeed itemSpeed) in _windowFastForwardSpeedMenuItems)
        {
            item.IsChecked = itemSpeed == speed;
        }
    }

    private void ConfigureWindowMenu()
    {
        OpenRomMenuItem.Click += (_, _) => OpenRomRequested?.Invoke(this, EventArgs.Empty);
        CloseWindowMenuItem.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        PauseEmulationMenuItem.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        ResetEmulationMenuItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        FastForwardMenuItem.InputGesture = _fastForwardGesture;
        FastForwardMenuItem.Click += (_, _) => FastForwardRequested?.Invoke(this, EventArgs.Empty);

        foreach (EmulationSpeed speed in Enum.GetValues<EmulationSpeed>())
        {
            var menuItem = new MenuItem
            {
                Header = speed.GetDisplayName(),
                ToggleType = MenuItemToggleType.CheckBox,
            };
            menuItem.Click += (_, _) =>
                FastForwardSpeedSelected?.Invoke(
                    this,
                    new FastForwardSpeedSelectedEventArgs(speed)
                );
            _windowFastForwardSpeedMenuItems.Add((menuItem, speed));
            FastForwardSpeedMenuItem.Items.Add(menuItem);
        }
    }

    private NativeMenu CreateNativeMenu()
    {
        var openMenuItem = new NativeMenuItem("Open ROM...")
        {
            Gesture = KeyGesture.Parse("Meta+O"),
        };
        openMenuItem.Click += (_, _) => OpenRomRequested?.Invoke(this, EventArgs.Empty);

        var closeMenuItem = new NativeMenuItem("Close") { Gesture = KeyGesture.Parse("Meta+W") };
        closeMenuItem.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        _nativePauseMenuItem.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        _nativeResetMenuItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        _nativeFastForwardMenuItem.Click += (_, _) =>
            FastForwardRequested?.Invoke(this, EventArgs.Empty);

        NativeMenuItem fastForwardSpeedMenuItem = new("Fast Forward Speed")
        {
            Menu = CreateNativeFastForwardSpeedMenu(),
        };

        return
        [
            new NativeMenuItem("File")
            {
                Menu = [openMenuItem, new NativeMenuItemSeparator(), closeMenuItem],
            },
            new NativeMenuItem("Emulation")
            {
                Menu =
                [
                    _nativePauseMenuItem,
                    _nativeResetMenuItem,
                    new NativeMenuItemSeparator(),
                    _nativeFastForwardMenuItem,
                    fastForwardSpeedMenuItem,
                ],
            },
        ];
    }

    private NativeMenu CreateNativeFastForwardSpeedMenu()
    {
        NativeMenu menu = [];

        foreach (EmulationSpeed speed in Enum.GetValues<EmulationSpeed>())
        {
            NativeMenuItem menuItem = new(speed.GetDisplayName())
            {
                ToggleType = MenuItemToggleType.CheckBox,
            };
            menuItem.Click += (_, _) =>
                FastForwardSpeedSelected?.Invoke(
                    this,
                    new FastForwardSpeedSelectedEventArgs(speed)
                );
            _nativeFastForwardSpeedMenuItems.Add((menuItem, speed));
            menu.Add(menuItem);
        }

        return menu;
    }
}
