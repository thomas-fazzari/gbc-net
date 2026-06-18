using GbcNet.App.Emulation;

namespace GbcNet.App.Menus;

internal sealed class FastForwardSpeedSelectedEventArgs(EmulationSpeed speed) : EventArgs
{
    public EmulationSpeed Speed { get; } = speed;
}
