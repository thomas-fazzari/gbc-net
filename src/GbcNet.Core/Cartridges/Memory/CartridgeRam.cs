using System.Globalization;
using FluentResults;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Cartridge external RAM storage, including battery-backed persistence state.
/// </summary>
internal sealed class CartridgeRam(int sizeBytes, bool hasBattery)
{
    private readonly byte[] _bytes = new byte[sizeBytes];
    private bool _dirty;

    /// <summary>
    /// Total external RAM capacity exposed by the cartridge hardware.
    /// </summary>
    public int Size => _bytes.Length;

    /// <summary>
    /// Indicates that this RAM should be persisted to a save file.
    /// </summary>
    public bool HasBatteryBackedRam => hasBattery && _bytes.Length != 0;

    /// <summary>
    /// Number of bytes exported for battery-backed save data.
    /// </summary>
    public int BatteryRamSize => HasBatteryBackedRam ? _bytes.Length : 0;

    /// <summary>
    /// Indicates that battery-backed RAM changed since the last import, export, or clear.
    /// </summary>
    public bool IsBatteryRamDirty => HasBatteryBackedRam && _dirty;

    /// <summary>
    /// Reads a byte from an already resolved external RAM offset.
    /// </summary>
    public byte Read(int offset) => _bytes[offset];

    /// <summary>
    /// Writes a byte to an already resolved external RAM offset and marks save data dirty.
    /// </summary>
    public void Write(int offset, byte value)
    {
        _bytes[offset] = value;
        _dirty |= HasBatteryBackedRam;
    }

    /// <summary>
    /// Exports a defensive copy of battery-backed RAM, or an empty array when unavailable.
    /// </summary>
    public byte[] ExportBatteryRam() => HasBatteryBackedRam ? (byte[])_bytes.Clone() : [];

    /// <summary>
    /// Imports battery-backed RAM and validates that the save length matches cartridge RAM size.
    /// </summary>
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

    /// <summary>
    /// Marks battery-backed RAM as clean after save data has been persisted.
    /// </summary>
    public void ClearBatteryRamDirty()
    {
        _dirty = false;
    }
}
