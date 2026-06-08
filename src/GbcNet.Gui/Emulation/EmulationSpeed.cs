namespace GbcNet.Gui.Emulation;

/// <summary>
/// Host allowed pacing multipliers.
/// </summary>
internal enum EmulationSpeed
{
    Normal = 10,

    OnePointFive = 15,

    Two = 20,

    TwoPointFive = 25,

    Three = 30,

    ThreePointFive = 35,

    Four = 40,

    Eight = 80,
}

internal static class EmulationSpeedExtensions
{
    internal static string GetDisplayName(this EmulationSpeed speed) =>
        speed switch
        {
            EmulationSpeed.Normal => "1x",
            EmulationSpeed.OnePointFive => "1.5x",
            EmulationSpeed.Two => "2x",
            EmulationSpeed.TwoPointFive => "2.5x",
            EmulationSpeed.Three => "3x",
            EmulationSpeed.ThreePointFive => "3.5x",
            EmulationSpeed.Four => "4x",
            EmulationSpeed.Eight => "8x",
            _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, message: null),
        };
}
