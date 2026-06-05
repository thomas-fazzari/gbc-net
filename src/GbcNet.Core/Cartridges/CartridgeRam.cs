using System.Globalization;
using FluentResults;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Cartridge external RAM storage, including battery-backed persistence state.
/// </summary>
internal sealed class CartridgeRam(int sizeBytes, bool hasBattery)
{
    private readonly byte[] _bytes = new byte[sizeBytes];
    private bool _dirty;

    public int Size => _bytes.Length;

    public bool HasBatteryBackedRam => hasBattery && _bytes.Length != 0;

    public int BatteryRamSize => HasBatteryBackedRam ? _bytes.Length : 0;

    public bool IsBatteryRamDirty => HasBatteryBackedRam && _dirty;

    public byte Read(int offset) => _bytes[offset];

    public void Write(int offset, byte value)
    {
        _bytes[offset] = value;
        _dirty |= HasBatteryBackedRam;
    }

    public byte[] ExportBatteryRam() => HasBatteryBackedRam ? (byte[])_bytes.Clone() : [];

    public Result ImportBatteryRam(ReadOnlySpan<byte> data)
    {
        if (!HasBatteryBackedRam)
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

        data.CopyTo(_bytes);
        _dirty = false;
        return Result.Ok();
    }

    public void ClearBatteryRamDirty()
    {
        _dirty = false;
    }
}
