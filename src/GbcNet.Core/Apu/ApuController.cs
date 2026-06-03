namespace GbcNet.Core.Apu;

/// <summary>
/// Stores CPU-visible Audio Processing Unit registers and applies DMG read masks.
/// </summary>
internal sealed class ApuController
{
    private const ushort RegisterStart = 0xFF10;
    private const ushort RegisterEnd = 0xFF26;

    // FF15 and FF1F sit inside the APU address range but are not real DMG registers.
    private const ushort UnmappedAudioAddressFf15 = 0xFF15;
    private const ushort UnmappedAudioAddressFf1F = 0xFF1F;

    private const ushort AudioMasterControlRegister = 0xFF26;
    private const byte AudioMasterWritableMask = 0x80;
    private const byte AudioMasterReadableMask = 0x8F;
    private const byte AudioChannelStatusMask = 0x0F;
    private const byte AudioMasterReadMask = 0x70;

    private readonly byte[] _registers = new byte[RegisterEnd - RegisterStart + 1];

    /// <summary>
    /// Returns whether an address is owned by the APU register block.
    /// </summary>
    internal static bool ContainsRegister(ushort address) =>
        address
            is >= RegisterStart
                and <= RegisterEnd
                and not UnmappedAudioAddressFf15
                and not UnmappedAudioAddressFf1F;

    /// <summary>
    /// Reads an APU register with unused and write-only bits forced high.
    /// </summary>
    public byte ReadRegister(ushort address)
    {
        byte value = _registers[address - RegisterStart];
        return address switch
        {
            0xFF10 => (byte)(value | 0x80),
            0xFF11 => (byte)(value | 0x3F),
            0xFF13 or 0xFF18 or 0xFF1B or 0xFF1D or 0xFF20 => 0xFF,
            0xFF14 or 0xFF19 or 0xFF1E or 0xFF23 => (byte)(value | 0xBF),
            0xFF16 => (byte)(value | 0x3F),
            0xFF1A => (byte)(value | 0x7F),
            0xFF1C => (byte)(value | 0x9F),
            AudioMasterControlRegister => (byte)(
                (value & AudioMasterReadableMask) | AudioMasterReadMask
            ),
            _ => value,
        };
    }

    /// <summary>
    /// Writes an APU register without generating sound or channel side effects yet.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        int registerIndex = address - RegisterStart;
        _registers[registerIndex] =
            address == AudioMasterControlRegister
                ? GetWrittenAudioMasterControl(value, _registers[registerIndex])
                : value;
    }

    /// <summary>
    /// Seeds an APU register without applying CPU write-only restrictions.
    /// </summary>
    internal void SetRegisterState(ushort address, byte value)
    {
        _registers[address - RegisterStart] = value;
    }

    private static byte GetWrittenAudioMasterControl(byte value, byte currentValue) =>
        (value & AudioMasterWritableMask) == 0
            ? (byte)0
            : (byte)((currentValue & AudioChannelStatusMask) | AudioMasterWritableMask);
}
