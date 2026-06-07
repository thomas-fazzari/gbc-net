using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GbcNet.Gui.Emulation;

/// <summary>
/// Host pacing multipliers exposed by the GUI.
/// </summary>
internal enum EmulationSpeed
{
    [Display(Name = "1x")]
    Normal = 10,

    [Display(Name = "1.5x")]
    OnePointFive = 15,

    [Display(Name = "2x")]
    Two = 20,

    [Display(Name = "2.5x")]
    TwoPointFive = 25,

    [Display(Name = "3x")]
    Three = 30,

    [Display(Name = "3.5x")]
    ThreePointFive = 35,

    [Display(Name = "4x")]
    Four = 40,
}

internal static class EmulationSpeedExtensions
{
    internal static string GetDisplayName(this EmulationSpeed speed)
    {
        string name =
            Enum.GetName(speed)
            ?? throw new ArgumentOutOfRangeException(nameof(speed), speed, message: null);

        MemberInfo member = typeof(EmulationSpeed).GetMember(name)[0];

        return member.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? name;
    }
}
