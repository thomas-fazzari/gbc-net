using System.Globalization;
using FluentResults;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC3 battery-backed save payload containing optional RAM followed by the standard RTC tail.
/// </summary>
internal sealed class Mbc3SaveData(CartridgeRam ram, Mbc3RealTimeClock realTimeClock)
    : ICartridgeSaveData
{
    public bool HasBatteryBackedSave => true;

    public int BatterySaveSize => ram.BatterySaveSize + Mbc3RealTimeClock.SaveStateSize;

    public bool IsBatterySaveDirty
    {
        get
        {
            realTimeClock.RefreshFromClock();
            return ram.IsBatterySaveDirty || realTimeClock.IsDirty;
        }
    }

    public byte[] ExportBatterySave()
    {
        byte[] data = new byte[BatterySaveSize];
        byte[] ramData = ram.ExportBatterySave();
        ramData.CopyTo(data.AsSpan(0, ramData.Length));
        realTimeClock.ExportState().CopyTo(data.AsSpan(ramData.Length));
        return data;
    }

    public Result ImportBatterySave(ReadOnlySpan<byte> data)
    {
        if (data.Length != BatterySaveSize)
        {
            return Result.Fail(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Save data length is {data.Length} bytes, but cartridge expects {BatterySaveSize} bytes."
                )
            );
        }

        int ramSize = ram.BatterySaveSize;
        Result ramImport = ram.ImportBatterySave(data[..ramSize]);
        if (ramImport.IsFailed)
        {
            return ramImport;
        }

        realTimeClock.ImportState(data.Slice(ramSize, Mbc3RealTimeClock.SaveStateSize));
        return Result.Ok();
    }

    public void ClearBatterySaveDirty()
    {
        ram.ClearBatterySaveDirty();
        realTimeClock.ClearDirty();
    }
}
