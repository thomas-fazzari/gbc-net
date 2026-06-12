namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Shared DMG pixel-rule renderer used by DMG hardware and CGB DMG compatibility output.
/// </summary>
internal abstract class DmgPixelRulesPpuEngine<TPixelOutput>()
    : PpuEngineBase(TPixelOutput.BytesPerPixel, TPixelOutput.PixelFormat)
    where TPixelOutput : struct, IDmgPixelOutput
{
    private readonly byte[] _backgroundFifo = new byte[BackgroundFifoCapacity];
    private readonly DmgObjectLayer _objects = new();
    private byte _fetcherTileId;

    protected override int ObjectPenaltyDots => _objects.PenaltyDots;

    protected override void EnsureObjectsSelected(PpuEngineInputs inputs)
    {
        _objects.EnsureSelected(
            inputs,
            LcdYCoordinate,
            Timing.HasReachedOamScanEnd,
            LatchedScrollXLowBits
        );
    }

    protected override void ClearObjects()
    {
        _objects.Clear();
    }

    protected override bool IsWindowEnabled(PpuEngineInputs inputs) =>
        (
            inputs.LcdControl
            & (
                PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask
                | PpuLcdControlRegister.WindowEnableMask
            )
        )
        == (
            PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask
            | PpuLcdControlRegister.WindowEnableMask
        );

    protected override void FetchTileMapEntry(PpuEngineInputs inputs, ushort tileMapAddress)
    {
        _fetcherTileId = inputs.VideoRam.Read(tileMapAddress);
    }

    protected override byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte) =>
        inputs.VideoRam.Read(GetTileDataAddress(inputs, highByte));

    protected override bool TryPushFetchedTileRow()
    {
        if (BackgroundFifoCount > PpuTileData.TileSizePixels)
        {
            return false;
        }

        for (var pixel = 0; pixel < PpuTileData.TileSizePixels; pixel++)
        {
            PushBackgroundPixel(
                PpuTileData.DecodeColorId(FetchedTileDataLow, FetchedTileDataHigh, 7 - pixel)
            );
        }

        return true;
    }

    protected override void TryRenderPixel(PpuEngineInputs inputs)
    {
        if (BackgroundFifoCount == 0 || RenderedPixels == PpuGeometry.FrameWidth)
        {
            return;
        }

        var colorId = PopBackgroundPixel();
        if (ShouldDiscardPixel())
        {
            return;
        }

        TPixelOutput.WritePixel(
            FrameBuffer,
            (LcdYCoordinate * PpuGeometry.FrameWidth) + RenderedPixels,
            DmgPixelRules.ApplyBackgroundEnable(colorId, inputs.LcdControl),
            _objects.SelectPixel(RenderedPixels, LcdYCoordinate, inputs),
            inputs
        );
        AdvanceRenderedPixel();
    }

    protected override void ClearFetchedTileMapEntry()
    {
        _fetcherTileId = 0;
    }

    private ushort GetTileDataAddress(PpuEngineInputs inputs, bool highByte) =>
        GetBackgroundTileDataAddress(
            inputs,
            _fetcherTileId,
            GetFetcherY() & TileLineMask,
            highByte
        );

    private void PushBackgroundPixel(byte colorId)
    {
        _backgroundFifo[BackgroundFifoWriteIndex] = colorId;
        CommitBackgroundFifoPush();
    }

    private byte PopBackgroundPixel()
    {
        var colorId = _backgroundFifo[BackgroundFifoReadIndex];
        CommitBackgroundFifoPop();
        return colorId;
    }
}

internal interface IDmgPixelOutput
{
    static abstract int BytesPerPixel { get; }

    static abstract LcdPixelFormat PixelFormat { get; }

    static abstract void WritePixel(
        byte[] frameBuffer,
        int pixelIndex,
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    );
}

internal readonly record struct DmgShadePixelOutput : IDmgPixelOutput
{
    public static int BytesPerPixel => 1;

    public static LcdPixelFormat PixelFormat => LcdPixelFormat.DmgShadeIndex8;

    public static void WritePixel(
        byte[] frameBuffer,
        int pixelIndex,
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        frameBuffer[pixelIndex] = DmgPixelRules.ResolveShade(
            backgroundColorId,
            objectPixel,
            inputs
        );
    }
}

internal readonly record struct CgbDmgCompatibilityPixelOutput : IDmgPixelOutput
{
    private const int Rgb555BytesPerPixel = 2;

    public static int BytesPerPixel => Rgb555BytesPerPixel;

    public static LcdPixelFormat PixelFormat => LcdPixelFormat.Rgb555Le;

    public static void WritePixel(
        byte[] frameBuffer,
        int pixelIndex,
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        var color = DmgPixelRules.ResolveCgbDmgCompatibilityColor(
            backgroundColorId,
            objectPixel,
            inputs
        );
        var frameOffset = pixelIndex * Rgb555BytesPerPixel;
        frameBuffer[frameOffset] = (byte)color;
        frameBuffer[frameOffset + 1] = (byte)(color >> 8);
    }
}

internal static class DmgPixelRules
{
    public static byte ApplyBackgroundEnable(byte backgroundColorId, byte lcdControl) =>
        (lcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) == 0
            ? (byte)0
            : backgroundColorId;

    public static byte ResolveShade(
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        if (
            objectPixel is null
            || BackgroundCoversObject(backgroundColorId, objectPixel.Value, inputs.LcdControl)
        )
        {
            return ApplyPalette(backgroundColorId, inputs.BackgroundPalette);
        }

        return ApplyPalette(
            objectPixel.Value.ColorId,
            objectPixel.Value.UsesPalette1 ? inputs.ObjectPalette1 : inputs.ObjectPalette0
        );
    }

    public static ushort ResolveCgbDmgCompatibilityColor(
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        if (
            objectPixel is null
            || BackgroundCoversObject(backgroundColorId, objectPixel.Value, inputs.LcdControl)
        )
        {
            return inputs.BackgroundPaletteRam.ReadRgb555Color(
                0,
                ApplyPalette(backgroundColorId, inputs.BackgroundPalette)
            );
        }

        var usesPalette1 = objectPixel.Value.UsesPalette1;
        return inputs.ObjectPaletteRam.ReadRgb555Color(
            usesPalette1 ? 1 : 0,
            ApplyPalette(
                objectPixel.Value.ColorId,
                usesPalette1 ? inputs.ObjectPalette1 : inputs.ObjectPalette0
            )
        );
    }

    private static bool BackgroundCoversObject(
        byte backgroundColorId,
        DmgObjectPixel objectPixel,
        byte lcdControl
    ) =>
        objectPixel.HasBackgroundPriority
        && backgroundColorId != 0
        && (lcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) != 0;

    private static byte ApplyPalette(byte colorId, byte palette) =>
        (byte)((palette >> (colorId * 2)) & 0x03);
}
