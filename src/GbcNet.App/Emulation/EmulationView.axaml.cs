using Avalonia.Controls;

namespace GbcNet.App.Emulation;

internal sealed partial class EmulationView : UserControl
{
    public EmulationView()
    {
        InitializeComponent();
    }

    public Image Screen => ScreenImage;
}
