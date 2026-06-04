namespace GbcNet.Core.Ppu;

/// <summary>
/// Background/window FIFO fetcher step.
/// </summary>
internal enum BackgroundFetcherStep
{
    GetTile = 0,
    GetTileDataLow = 1,
    GetTileDataHigh = 2,
    Sleep = 3,
    Push = 4,
}
