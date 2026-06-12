namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB LCD engine for BG/window/OBJ RGB555 rendering with CGB tile attributes.
/// </summary>
internal sealed class CgbPpuEngine() : PpuEngineBase(Rgb555BytesPerPixel, LcdPixelFormat.Rgb555Le)
{
    private const byte AttributePaletteMask = 0x07;
    private const byte AttributeTileBankMask = 0x08;
    private const byte AttributeXFlipMask = 0x20;
    private const byte AttributeYFlipMask = 0x40;
    private const byte AttributeBackgroundPriorityMask = 0x80;
    private const int Rgb555BytesPerPixel = 2;

    private readonly byte[] _backgroundColorFifo = new byte[BackgroundFifoCapacity];
    private readonly byte[] _backgroundAttributeFifo = new byte[BackgroundFifoCapacity];
    private readonly CgbObjectLayer _objects = new();
    private byte _fetcherTileId;
    private byte _fetcherTileAttributes;

    protected override int ObjectPenaltyDots => 0;

    protected override void EnsureObjectsSelected(PpuEngineInputs inputs)
    {
        _objects.EnsureSelected(inputs, LcdYCoordinate, Timing.HasReachedOamScanEnd);
    }

    protected override void ClearObjects()
    {
        _objects.Clear();
    }

    protected override bool IsWindowEnabled(PpuEngineInputs inputs) =>
        (inputs.LcdControl & PpuLcdControlRegister.WindowEnableMask) != 0;

    protected override void FetchTileMapEntry(PpuEngineInputs inputs, ushort tileMapAddress)
    {
        _fetcherTileId = inputs.VideoRam.ReadBank(bank: 0, tileMapAddress);
        _fetcherTileAttributes = inputs.VideoRam.ReadBank(bank: 1, tileMapAddress);
    }

    protected override byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte)
    {
        var tileBank = (_fetcherTileAttributes & AttributeTileBankMask) == 0 ? 0 : 1;
        return inputs.VideoRam.ReadBank(tileBank, GetTileDataAddress(inputs, highByte));
    }

    protected override bool TryPushFetchedTileRow()
    {
        if (BackgroundFifoCount > PpuTileData.TileSizePixels)
        {
            return false;
        }

        var xFlip = (_fetcherTileAttributes & AttributeXFlipMask) != 0;
        for (var pixel = 0; pixel < PpuTileData.TileSizePixels; pixel++)
        {
            PushBackgroundPixel(
                PpuTileData.DecodeColorId(
                    FetchedTileDataLow,
                    FetchedTileDataHigh,
                    xFlip ? pixel : 7 - pixel
                ),
                _fetcherTileAttributes
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

        PopBackgroundPixel(out var colorId, out var attributes);
        if (ShouldDiscardPixel())
        {
            return;
        }

        WriteRgb555Pixel(MixPixel(colorId, attributes, inputs));
        AdvanceRenderedPixel();
    }

    protected override void ClearFetchedTileMapEntry()
    {
        _fetcherTileId = 0;
        _fetcherTileAttributes = 0;
    }

    private ushort GetTileDataAddress(PpuEngineInputs inputs, bool highByte)
    {
        var tileLine = GetFetcherY() & TileLineMask;
        if ((_fetcherTileAttributes & AttributeYFlipMask) != 0)
        {
            tileLine = PpuTileData.TileSizePixels - 1 - tileLine;
        }

        return GetBackgroundTileDataAddress(inputs, _fetcherTileId, tileLine, highByte);
    }

    private ushort MixPixel(
        byte backgroundColorId,
        byte backgroundAttributes,
        PpuEngineInputs inputs
    )
    {
        var objectPixel = _objects.SelectPixel(RenderedPixels, LcdYCoordinate, inputs);
        if (
            objectPixel is null
            || BackgroundCoversObject(
                backgroundColorId,
                backgroundAttributes,
                objectPixel.Value,
                inputs.LcdControl
            )
        )
        {
            return ResolveBackgroundColor(backgroundColorId, backgroundAttributes, inputs);
        }

        return ResolveObjectColor(objectPixel.Value, inputs);
    }

    private static bool BackgroundCoversObject(
        byte backgroundColorId,
        byte backgroundAttributes,
        CgbObjectPixel objectPixel,
        byte lcdControl
    ) =>
        backgroundColorId != 0
        && (lcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) != 0
        && (
            (backgroundAttributes & AttributeBackgroundPriorityMask) != 0
            || objectPixel.HasBackgroundPriority
        );

    private static ushort ResolveBackgroundColor(
        byte backgroundColorId,
        byte backgroundAttributes,
        PpuEngineInputs inputs
    ) =>
        inputs.BackgroundPaletteRam.ReadRgb555Color(
            backgroundAttributes & AttributePaletteMask,
            backgroundColorId
        );

    private static ushort ResolveObjectColor(CgbObjectPixel objectPixel, PpuEngineInputs inputs) =>
        inputs.ObjectPaletteRam.ReadRgb555Color(objectPixel.PaletteIndex, objectPixel.ColorId);

    private void WriteRgb555Pixel(ushort color)
    {
        var frameOffset =
            ((LcdYCoordinate * PpuGeometry.FrameWidth) + RenderedPixels) * Rgb555BytesPerPixel;
        FrameBuffer[frameOffset] = (byte)color;
        FrameBuffer[frameOffset + 1] = (byte)(color >> 8);
    }

    private void PushBackgroundPixel(byte colorId, byte attributes)
    {
        var writeIndex = BackgroundFifoWriteIndex;
        _backgroundColorFifo[writeIndex] = colorId;
        _backgroundAttributeFifo[writeIndex] = attributes;
        CommitBackgroundFifoPush();
    }

    private void PopBackgroundPixel(out byte colorId, out byte attributes)
    {
        var readIndex = BackgroundFifoReadIndex;
        colorId = _backgroundColorFifo[readIndex];
        attributes = _backgroundAttributeFifo[readIndex];
        CommitBackgroundFifoPop();
    }
}
