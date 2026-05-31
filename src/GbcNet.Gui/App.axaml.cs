using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace GbcNet.Gui;

internal sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow =
            new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
