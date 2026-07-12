// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

internal readonly record struct PpuEngineBaseState(
    PpuTimingState Timing,
    PpuStatInterruptLatchState StatInterruptLatch,
    BackgroundWindowFetcherState BackgroundWindowFetcher,
    byte[] FrameBuffer,
    int RenderedPixels,
    bool RenderingScanline,
    bool RenderCurrentFrame
);

/// <summary>
/// Shared LCD timing and render orchestration for the current PPU engines.
/// </summary>
internal abstract class PpuEngineBase(int frameBufferBytesPerPixel, LcdPixelFormat framePixelFormat)
    : IPpuEngine
{
    protected const int TileLineMask = PpuTileData.TileSizePixels - 1;
    protected const int BackgroundFifoCapacity = PpuTileData.TileSizePixels * 2;

    /// <summary>
    /// CGB raises the LY=144 Mode 2 STAT interrupt one M-cycle before VBlank.
    /// </summary>
    private const int CgbMode2VBlankInterruptLeadDots = 4;

    protected BackgroundWindowFetcher BgWindowFetcher { get; } = new();
    private readonly PpuStatInterruptState _statInterruptState = new();
    private bool _renderingScanline;
    private bool _renderCurrentFrame = true;

    public byte LcdYCoordinate => Timing.LcdYCoordinate;

    public bool LycEqualsLy => _statInterruptState.LycEqualsLy;

    public PpuMode StatusMode => Timing.StatusMode;

    public bool IsCpuVideoRamReadBlocked =>
        Timing.IsCpuVideoRamReadBlocked(GetCurrentDrawingEndDots());

    public bool IsCpuVideoRamWriteBlocked =>
        Timing.IsCpuVideoRamWriteBlocked(
            Timing.GetCurrentDrawingStartDots(),
            GetCurrentDrawingEndDots()
        );

    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        Timing.IsCpuObjectAttributeMemoryReadBlocked(GetCurrentDrawingEndDots());

    public bool IsCpuObjectAttributeMemoryWriteBlocked =>
        Timing.IsCpuObjectAttributeMemoryWriteBlocked(
            Timing.GetCurrentDrawingStartDots(),
            GetCurrentDrawingEndDots()
        );

    protected byte[] FrameBuffer { get; } =
        new byte[PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * frameBufferBytesPerPixel];

    protected PpuTiming Timing { get; } = new();

    protected int RenderedPixels { get; set; }

    public abstract IPpuEngineState CaptureState();

    public abstract void ValidateState(IPpuEngineState state);

    public abstract void RestoreState(IPpuEngineState state);

    protected PpuEngineBaseState CapturePpuEngineBaseState() =>
        new(
            Timing.CaptureState(),
            _statInterruptState.CaptureState(),
            BgWindowFetcher.CaptureState(),
            [.. FrameBuffer],
            RenderedPixels,
            _renderingScanline,
            _renderCurrentFrame
        );

    protected void ValidatePpuEngineBaseState(PpuEngineBaseState state)
    {
        PpuTiming.ValidateState(state.Timing);
        PpuStatInterruptState.ValidateState(state.StatInterruptLatch);
        BackgroundWindowFetcher.ValidateState(state.BackgroundWindowFetcher);

        if (state.FrameBuffer is null || state.FrameBuffer.Length != FrameBuffer.Length)
        {
            throw new ArgumentException(
                "Frame buffer length must match this engine's output format.",
                nameof(state)
            );
        }

        if (state.RenderedPixels is < 0 or > PpuGeometry.FrameWidth)
        {
            throw new ArgumentException(
                "Rendered pixels must be within the scanline width.",
                nameof(state)
            );
        }
    }

    protected void RestorePpuEngineBaseState(PpuEngineBaseState state)
    {
        ValidatePpuEngineBaseState(state);
        Timing.RestoreState(state.Timing);
        _statInterruptState.RestoreState(state.StatInterruptLatch);
        BgWindowFetcher.RestoreState(state.BackgroundWindowFetcher);
        state.FrameBuffer.CopyTo(FrameBuffer, 0);
        RenderedPixels = state.RenderedPixels;
        _renderingScanline = state.RenderingScanline;
        _renderCurrentFrame = state.RenderCurrentFrame;
    }

    protected abstract int ObjectPenaltyDots { get; }

    protected virtual bool RequestsMode2InterruptBeforeVBlank => false;

    public PpuEngineTickResult Tick(int tCycles, PpuEngineInputs inputs, bool renderFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (tCycles == 0)
        {
            return new PpuEngineTickResult(
                PpuInterruptRequest.None,
                CompletedFrame: null,
                EnteredVisibleHBlank: false
            );
        }

        var requests = RefreshPpuState(inputs, requestInterrupt: true);
        LcdFrame? completedFrame = null;
        var enteredVisibleHBlank = false;
        var remainingCycles = tCycles;
        while (remainingCycles > 0)
        {
            var scanlineDots = Timing.GetCurrentScanlineDots();
            var drawingStartDots = Timing.GetCurrentDrawingStartDots();
            var drawingEndDots = GetCurrentDrawingEndDots();
            var nextBoundary = Timing.GetNextTimingBoundary(drawingEndDots);
            var cgbMode2VBlankInterruptDots = scanlineDots - CgbMode2VBlankInterruptLeadDots;
            if (
                RequestsMode2InterruptBeforeVBlank
                && LcdYCoordinate == PpuGeometry.VBlankStartLine - 1
                && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0
                && !_statInterruptState.IsInterruptLineAsserted
                && Timing.LineDots < cgbMode2VBlankInterruptDots
                && nextBoundary > cgbMode2VBlankInterruptDots
            )
            {
                nextBoundary = cgbMode2VBlankInterruptDots;
            }

            var elapsedCycles = Math.Min(remainingCycles, nextBoundary - Timing.LineDots);
            if (Timing.IsRenderingInterval(drawingStartDots, drawingEndDots))
            {
                if (_renderCurrentFrame)
                {
                    TickRenderer(elapsedCycles, inputs);
                }
                else
                {
                    TickVideoTimingOnly(inputs);
                }
            }

            Timing.AdvanceDots(elapsedCycles);
            remainingCycles -= elapsedCycles;

            if (Timing.LineDots == scanlineDots)
            {
                var result = AdvanceScanline(inputs, renderFrame);
                requests |= result.Interrupts;
                completedFrame ??= result.CompletedFrame;
                enteredVisibleHBlank |= result.EnteredVisibleHBlank;
                continue;
            }

            var previousMode = StatusMode;
            var previousLcdYCoordinate = LcdYCoordinate;
            requests |= RefreshPpuState(inputs, requestInterrupt: true);
            if (
                RequestsMode2InterruptBeforeVBlank
                && LcdYCoordinate == PpuGeometry.VBlankStartLine - 1
                && Timing.LineDots == cgbMode2VBlankInterruptDots
                && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0
                && !_statInterruptState.IsInterruptLineAsserted
            )
            {
                requests |= PpuInterruptRequest.LcdStat;
            }

            enteredVisibleHBlank |=
                previousLcdYCoordinate < PpuGeometry.VBlankStartLine
                && previousMode is PpuMode.Drawing
                && StatusMode is PpuMode.HBlank;
        }

        return new PpuEngineTickResult(requests, completedFrame, enteredVisibleHBlank);
    }

    public PpuInterruptRequest EnableLcd(PpuEngineInputs inputs, bool renderFrame)
    {
        Timing.EnableLcd();
        BgWindowFetcher.LatchScroll(inputs);
        BgWindowFetcher.RefreshWindowYCondition(inputs, LcdYCoordinate);
        ClearObjects();
        ResetRenderer();
        _renderCurrentFrame = renderFrame;
        _statInterruptState.SetMode(PpuMode.HBlank);

        var oldLycEqualsLy = LycEqualsLy;
        _statInterruptState.RefreshLycEqualsLy(Timing, inputs.LcdYCompare);

        if (
            !_statInterruptState.ShouldSuppressStableLycInterrupt(
                oldLycEqualsLy,
                inputs.StatusInterruptSelect
            )
        )
        {
            return _statInterruptState.RefreshInterruptLine(
                inputs.StatusInterruptSelect,
                lcdEnabled: true,
                requestInterrupt: true
            );
        }

        _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt: false
        );
        return PpuInterruptRequest.None;
    }

    public void DisableLcd()
    {
        Timing.DisableLcd();
        BgWindowFetcher.ResetForLcdDisable();
        _renderCurrentFrame = true;
        ClearObjects();
        ResetRenderer();
        _statInterruptState.ClearInterruptLine(PpuMode.HBlank);
    }

    public PpuInterruptRequest WriteStatusInterruptSelect(
        PpuEngineInputs inputs,
        bool lcdEnabled
    ) =>
        _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled,
            requestInterrupt: true
        );

    public PpuInterruptRequest WriteLycCompare(PpuEngineInputs inputs, bool lcdEnabled)
    {
        if (!lcdEnabled)
        {
            return PpuInterruptRequest.None;
        }

        _statInterruptState.RefreshLycEqualsLy(Timing, inputs.LcdYCompare);

        return _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt: true
        );
    }

    public void SetStatusState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        _statInterruptState.SetLycEqualsLyFromStatus(value);
        Timing.SetStatusState(value);
        _statInterruptState.SetMode(StatusMode);
        _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled,
            requestInterrupt: false
        );
    }

    public void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        Timing.SetLcdYCoordinateState(value);
        if (LcdYCoordinate < PpuGeometry.VBlankStartLine)
        {
            BgWindowFetcher.LatchScroll(inputs);
            BgWindowFetcher.RefreshWindowYCondition(inputs, LcdYCoordinate);
            ClearObjects();
        }
        else
        {
            BgWindowFetcher.ClearWindowPenalty();
            ClearObjects();
        }

        _statInterruptState.RefreshLycEqualsLy(Timing, inputs.LcdYCompare);
        _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled,
            requestInterrupt: false
        );
    }

    public void SetLycCompareState(PpuEngineInputs inputs, bool lcdEnabled)
    {
        _statInterruptState.RefreshLycEqualsLy(Timing, inputs.LcdYCompare);
        _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled,
            requestInterrupt: false
        );
    }

    protected abstract void EnsureObjectsSelected(PpuEngineInputs inputs);

    protected abstract void ClearObjects();

    internal abstract bool IsWindowEnabled(PpuEngineInputs inputs);

    internal abstract void FetchTileMapEntry(PpuEngineInputs inputs, ushort tileMapAddress);

    internal abstract byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte);

    internal abstract bool TryPushFetchedTileRow();

    protected abstract void TryRenderPixel(PpuEngineInputs inputs);

    internal abstract void ClearFetchedTileMapEntry();

    private LcdFrame CreateCompletedFrame() =>
        new(PpuGeometry.FrameWidth, PpuGeometry.FrameHeight, framePixelFormat, FrameBuffer);

    private int GetCurrentDrawingEndDots() =>
        Timing.GetCurrentDrawingEndDots(
            BgWindowFetcher.LatchedScrollXLowBits,
            BgWindowFetcher.WindowPenaltyDots,
            ObjectPenaltyDots
        );

    private PpuEngineTickResult AdvanceScanline(PpuEngineInputs inputs, bool renderFrame)
    {
        Timing.AdvanceScanline();
        var requests = PpuInterruptRequest.None;
        LcdFrame? completedFrame = null;

        var shouldRequestMode2Interrupt =
            !_statInterruptState.IsInterruptLineAsserted
            && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0;

        switch (LcdYCoordinate)
        {
            case < PpuGeometry.VBlankStartLine:
                if (LcdYCoordinate == 0)
                {
                    _renderCurrentFrame = renderFrame;
                }

                BgWindowFetcher.LatchScroll(inputs);
                BgWindowFetcher.RefreshWindowYCondition(inputs, LcdYCoordinate);
                ClearObjects();
                ResetRenderer();
                break;

            case PpuGeometry.VBlankStartLine:
                ClearObjects();
                BgWindowFetcher.ResetForVBlank();
                ResetRenderer();
                requests |= PpuInterruptRequest.VBlank;
                if (_renderCurrentFrame)
                {
                    completedFrame = CreateCompletedFrame();
                }
                break;
        }

        if (shouldRequestMode2Interrupt && LcdYCoordinate is 0 or PpuGeometry.VBlankStartLine)
        {
            requests |= PpuInterruptRequest.LcdStat;
        }

        requests |= RefreshPpuState(inputs, requestInterrupt: true);

        return new PpuEngineTickResult(requests, completedFrame, EnteredVisibleHBlank: false);
    }

    private PpuInterruptRequest RefreshPpuState(PpuEngineInputs inputs, bool requestInterrupt)
    {
        EnsureObjectsSelected(inputs);
        _statInterruptState.SetMode(Timing.RefreshStatusMode(GetCurrentDrawingEndDots()));

        _statInterruptState.RefreshLycEqualsLy(Timing, inputs.LcdYCompare);
        return _statInterruptState.RefreshInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt
        );
    }

    private void TickVideoTimingOnly(PpuEngineInputs inputs)
    {
        EnsureObjectsSelected(inputs);

        if (_renderingScanline)
        {
            return;
        }

        BeginRenderingScanline();
        BgWindowFetcher.TryStartWindowForTimingOnly(inputs, this);
    }

    private void TickRenderer(int dots, PpuEngineInputs inputs)
    {
        EnsureObjectsSelected(inputs);

        if (!_renderingScanline)
        {
            BeginRenderingScanline();
        }

        for (var dot = 0; dot < dots && RenderedPixels < PpuGeometry.FrameWidth; dot++)
        {
            BgWindowFetcher.TryStartWindow(inputs, RenderedPixels, this);
            BgWindowFetcher.Advance(inputs, LcdYCoordinate, this);
            TryRenderPixel(inputs);
        }
    }

    private void BeginRenderingScanline()
    {
        ResetRenderer();
        _renderingScanline = true;
    }

    private void ResetRenderer()
    {
        RenderedPixels = 0;
        BgWindowFetcher.ResetRenderer(this);
        _renderingScanline = false;
    }
}
