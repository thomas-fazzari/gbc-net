using FluentResults;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Battery-save-facing cartridge RAM storage.
/// </summary>
internal interface ICartridgeRamStorage
{
    /// <summary>
    /// Indicates that this RAM should be persisted to a save file.
    /// </summary>
    bool HasBatteryBackedRam { get; }

    /// <summary>
    /// Number of bytes exported for battery-backed save data.
    /// </summary>
    int BatteryRamSize { get; }

    /// <summary>
    /// Indicates that battery-backed RAM changed since the last import, export, or clear.
    /// </summary>
    bool IsBatteryRamDirty { get; }

    /// <summary>
    /// Exports a defensive copy of battery-backed RAM, or an empty array when unavailable.
    /// </summary>
    byte[] ExportBatteryRam();

    /// <summary>
    /// Imports battery-backed RAM and validates that the save length matches cartridge RAM size.
    /// </summary>
    Result ImportBatteryRam(ReadOnlySpan<byte> data);

    /// <summary>
    /// Marks battery-backed RAM as clean after save data has been persisted.
    /// </summary>
    void ClearBatteryRamDirty();
}
