using FluentResults;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Battery-backed cartridge persistence payload.
/// </summary>
internal interface ICartridgeSaveData
{
    /// <summary>
    /// Indicates that this cartridge has battery-backed data to persist.
    /// </summary>
    bool HasBatteryBackedSave { get; }

    /// <summary>
    /// Number of bytes exported for the battery-backed save payload.
    /// </summary>
    int BatterySaveSize { get; }

    /// <summary>
    /// Indicates that battery-backed state changed since the last import or clear.
    /// </summary>
    bool IsBatterySaveDirty { get; }

    /// <summary>
    /// Exports a defensive copy of battery-backed state, or an empty array when unavailable.
    /// </summary>
    byte[] ExportBatterySave();

    /// <summary>
    /// Imports battery-backed state and validates the payload format and length.
    /// </summary>
    Result ImportBatterySave(ReadOnlySpan<byte> data);

    /// <summary>
    /// Marks battery-backed state as clean after it has been persisted.
    /// </summary>
    void ClearBatterySaveDirty();
}
