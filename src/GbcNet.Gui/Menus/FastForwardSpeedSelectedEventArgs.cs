using GbcNet.Gui.Emulation;

namespace GbcNet.Gui.Menus;

internal sealed class FastForwardSpeedSelectedEventArgs(EmulationSpeed speed) : EventArgs
{
    public EmulationSpeed Speed { get; } = speed;
}
