using System.Globalization;
using FluentResults;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC2 built-in 512 x 4-bit RAM with optional battery-backed persistence.
/// </summary>
internal sealed class Mbc2Ram(bool hasBattery) : ICartridgeSaveData
{
    private const int RamSize = 512;
    private const byte StoredNibbleMask = 0x0F;
    private const byte ReadHighNibbleMask = 0xF0;

    private readonly byte[] _bytes = new byte[RamSize];
    private bool _dirty;

    public bool HasBatteryBackedSave => hasBattery;

    public int BatterySaveSize => hasBattery ? _bytes.Length : 0;

    public bool IsBatterySaveDirty => hasBattery && _dirty;

    /// <summary>
    /// Reads a stored nibble as an MBC2 RAM byte with high bits set.
    /// </summary>
    public byte Read(int offset) => (byte)(_bytes[offset] | ReadHighNibbleMask);

    /// <summary>
    /// Stores the low nibble and marks battery-backed RAM dirty.
    /// </summary>
    public void Write(int offset, byte value)
    {
        _bytes[offset] = (byte)(value & StoredNibbleMask);
        _dirty |= hasBattery;
    }

    public byte[] ExportBatterySave() => hasBattery ? (byte[])_bytes.Clone() : [];

    public Result ImportBatterySave(ReadOnlySpan<byte> data)
    {
        if (!hasBattery)
        {
            return data.IsEmpty ? Result.Ok() : Result.Fail("Cartridge has no battery-backed RAM.");
        }

        if (data.Length != _bytes.Length)
        {
            return Result.Fail(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Save RAM length is {0} bytes, but cartridge expects {1} bytes.",
                    data.Length,
                    _bytes.Length
                )
            );
        }

        for (int index = 0; index < data.Length; index++)
        {
            _bytes[index] = (byte)(data[index] & StoredNibbleMask);
        }

        _dirty = false;
        return Result.Ok();
    }

    public void ClearBatterySaveDirty()
    {
        _dirty = false;
    }
}
