using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Applies hardware register state used when boot ROM execution is skipped.
/// </summary>
internal static class PostBootState
{
    /// <summary>
    /// Seeds CPU registers and IO registers through the selected hardware profile.
    /// </summary>
    public static void Apply(
        IHardwareProfile hardwareProfile,
        Cartridge cartridge,
        Cpu cpu,
        MemoryBus bus
    )
    {
        ArgumentNullException.ThrowIfNull(hardwareProfile);

        hardwareProfile.ApplyPostBootState(cartridge, cpu, bus);
    }

    internal static void SetCpuRegisters(Registers registers, PostBootCpuRegisterState state)
    {
        registers.A = state.A;
        registers.F = state.F;
        registers.BC = state.BC;
        registers.DE = state.DE;
        registers.HL = state.HL;
        registers.PC = state.PC;
        registers.SP = state.SP;
    }

    internal static void SetHardwareRegisterStates(
        MemoryBus bus,
        ReadOnlySpan<PostBootHardwareRegisterState> registerStates
    )
    {
        foreach (var registerState in registerStates)
        {
            bus.SetHardwareRegisterState(registerState.Address, registerState.RegisterValue);
        }
    }
}

internal readonly record struct PostBootCpuRegisterState(
    byte A,
    byte F,
    ushort BC,
    ushort DE,
    ushort HL,
    ushort PC,
    ushort SP
);

internal readonly record struct PostBootHardwareRegisterState(ushort Address, byte RegisterValue);
