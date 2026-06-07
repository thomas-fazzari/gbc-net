using System.Globalization;
using FluentResults;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Cartridge RAM storage, including battery-backed persistence state.
/// </summary>
internal sealed class CartridgeRam(int sizeBytes, bool hasBattery) : ICartridgeSaveData
{
    private readonly byte[] _bytes = new byte[sizeBytes];
    private bool _dirty;

    /// <summary>
    /// Total cartridge RAM capacity exposed by the cartridge hardware.
    /// </summary>
    public int Size => _bytes.Length;

    /// <summary>
    /// Indicates that this RAM should be persisted to a save file.
    /// </summary>
    public bool HasBatteryBackedSave => hasBattery && _bytes.Length != 0;

    /// <summary>
    /// Number of bytes exported for battery-backed save data.
    /// </summary>
    public int BatterySaveSize => HasBatteryBackedSave ? _bytes.Length : 0;

    /// <summary>
    /// Indicates that battery-backed RAM changed since the last import or clear.
    /// </summary>
    public bool IsBatterySaveDirty => HasBatteryBackedSave && _dirty;

    /// <summary>
    /// Reads a byte from an already resolved cartridge RAM offset.
    /// </summary>
    public byte Read(int offset) => _bytes[offset];

    /// <summary>
    /// Writes a byte to an already resolved cartridge RAM offset and marks save data dirty.
    /// </summary>
    public void Write(int offset, byte value)
    {
        _bytes[offset] = value;
        _dirty |= HasBatteryBackedSave;
    }

    /// <summary>
    /// Exports a defensive copy of battery-backed RAM, or an empty array when unavailable.
    /// </summary>
    public byte[] ExportBatterySave() => HasBatteryBackedSave ? (byte[])_bytes.Clone() : [];

    /// <summary>
    /// Imports battery-backed RAM and validates that the save length matches cartridge RAM size.
    /// </summary>
    public Result ImportBatterySave(ReadOnlySpan<byte> data)
    {
        if (!HasBatteryBackedSave)
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
    public void ClearBatterySaveDirty()
    {
        _dirty = false;
    }
}
