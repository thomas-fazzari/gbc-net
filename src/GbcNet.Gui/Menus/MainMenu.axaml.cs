using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.Gui.Emulation;

namespace GbcNet.Gui.Menus;

internal sealed partial class MainMenu : UserControl
{
    private static readonly KeyGesture _fullscreenGesture = KeyGesture.Parse("Alt+Enter");
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

    private readonly NativeMenuItem _nativeFullscreenMenuItem = new("Fullscreen")
    {
        Gesture = _fullscreenGesture,
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

    public event EventHandler? FullscreenRequested;

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
        var header = isPaused ? "Resume" : "Pause";

        _nativePauseMenuItem.Header = header;
        _nativePauseMenuItem.IsEnabled = isEnabled;

        PauseEmulationMenuItem.Header = header;
        PauseEmulationMenuItem.IsEnabled = isEnabled;
    }

    public void SetFastForwardState(bool isEnabled, EmulationSpeed speed)
    {
        _nativeFastForwardMenuItem.IsChecked = isEnabled;
        FastForwardMenuItem.IsChecked = isEnabled;

        foreach (var (item, itemSpeed) in _nativeFastForwardSpeedMenuItems)
        {
            item.IsChecked = itemSpeed == speed;
        }

        foreach (var (item, itemSpeed) in _windowFastForwardSpeedMenuItems)
        {
            item.IsChecked = itemSpeed == speed;
        }
    }

    public void SetFullscreenState(bool isFullscreen)
    {
        _nativeFullscreenMenuItem.IsChecked = isFullscreen;
        FullscreenMenuItem.IsChecked = isFullscreen;
    }

    #region Window menu
    private void ConfigureWindowMenu()
    {
        ConfigureWindowFileMenu();
        ConfigureWindowEmulationMenu();
        ConfigureWindowViewMenu();
    }

    private void ConfigureWindowFileMenu()
    {
        OpenRomMenuItem.Click += (_, _) => OpenRomRequested?.Invoke(this, EventArgs.Empty);
        CloseWindowMenuItem.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureWindowEmulationMenu()
    {
        PauseEmulationMenuItem.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        ResetEmulationMenuItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        FastForwardMenuItem.InputGesture = _fastForwardGesture;
        FastForwardMenuItem.Click += (_, _) => FastForwardRequested?.Invoke(this, EventArgs.Empty);

        foreach (var speed in Enum.GetValues<EmulationSpeed>())
        {
            var item = CreateWindowFastForwardSpeedMenuItem(speed);
            _windowFastForwardSpeedMenuItems.Add((item, speed));
            FastForwardSpeedMenuItem.Items.Add(item);
        }
    }

    private void ConfigureWindowViewMenu()
    {
        FullscreenMenuItem.InputGesture = _fullscreenGesture;
        FullscreenMenuItem.Click += (_, _) => FullscreenRequested?.Invoke(this, EventArgs.Empty);
    }

    private MenuItem CreateWindowFastForwardSpeedMenuItem(EmulationSpeed speed)
    {
        var item = new MenuItem
        {
            Header = speed.GetDisplayName(),
            ToggleType = MenuItemToggleType.CheckBox,
        };
        item.Click += (_, _) =>
            FastForwardSpeedSelected?.Invoke(this, new FastForwardSpeedSelectedEventArgs(speed));
        return item;
    }

    #endregion Window menu

    #region Native menu
    private NativeMenu CreateNativeMenu() =>
        [
            new NativeMenuItem("File") { Menu = CreateNativeFileMenu() },
            new NativeMenuItem("Emulation") { Menu = CreateNativeEmulationMenu() },
            new NativeMenuItem("View") { Menu = CreateNativeViewMenu() },
        ];

    private NativeMenu CreateNativeFileMenu()
    {
        var openItem = new NativeMenuItem("Open ROM...") { Gesture = KeyGesture.Parse("Meta+O") };
        openItem.Click += (_, _) => OpenRomRequested?.Invoke(this, EventArgs.Empty);

        var closeItem = new NativeMenuItem("Close") { Gesture = KeyGesture.Parse("Meta+W") };
        closeItem.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        return [openItem, new NativeMenuItemSeparator(), closeItem];
    }

    private NativeMenu CreateNativeEmulationMenu()
    {
        _nativePauseMenuItem.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        _nativeResetMenuItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        _nativeFastForwardMenuItem.Click += (_, _) =>
            FastForwardRequested?.Invoke(this, EventArgs.Empty);

        return
        [
            _nativePauseMenuItem,
            _nativeResetMenuItem,
            new NativeMenuItemSeparator(),
            _nativeFastForwardMenuItem,
            new NativeMenuItem("Fast Forward Speed") { Menu = CreateNativeFastForwardSpeedMenu() },
        ];
    }

    private NativeMenu CreateNativeViewMenu()
    {
        _nativeFullscreenMenuItem.Click += (_, _) =>
            FullscreenRequested?.Invoke(this, EventArgs.Empty);

        return [_nativeFullscreenMenuItem];
    }

    private NativeMenu CreateNativeFastForwardSpeedMenu()
    {
        NativeMenu menu = [];

        foreach (var speed in Enum.GetValues<EmulationSpeed>())
        {
            var item = CreateNativeFastForwardSpeedMenuItem(speed);
            _nativeFastForwardSpeedMenuItems.Add((item, speed));
            menu.Add(item);
        }

        return menu;
    }

    private NativeMenuItem CreateNativeFastForwardSpeedMenuItem(EmulationSpeed speed)
    {
        NativeMenuItem item = new(speed.GetDisplayName())
        {
            ToggleType = MenuItemToggleType.CheckBox,
        };
        item.Click += (_, _) =>
            FastForwardSpeedSelected?.Invoke(this, new FastForwardSpeedSelectedEventArgs(speed));
        return item;
    }
    #endregion Native menu
}
