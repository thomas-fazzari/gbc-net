using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Engines;

namespace GbcNet.Tests.Ppu;

public sealed class PpuControllerTests
{
    private const byte LcdEnable = 0x80;
    private const byte StatusModeMask = 0x03;
    private const byte LcdInterruptMask = 0b0000_0010;
    private const byte VBlankInterruptMask = 0b0000_0001;
    private const byte LcdYCompareStatusMask = 0x04;
    private const byte BackgroundEnable = 0x01;
    private const byte ObjectEnable = 0x02;
    private const byte ObjectSize16 = 0x04;
    private const byte UnsignedBackgroundTileData = 0x10;
    private const byte WindowEnable = 0x20;
    private const byte WindowTileMap1 = 0x40;
    private const byte IdentityPalette = 0xE4;
    private const byte PaletteColorOneToDarkGray = 0x08;
    private const byte PaletteColorOneToBlack = 0x0C;
    private const byte ObjectPalette1 = 0x10;
    private const byte ObjectXFlip = 0x20;
    private const byte ObjectYFlip = 0x40;
    private const byte ObjectBehindBackground = 0x80;
    public static TheoryData<ushort, byte> ReadWriteRegisters =>
        new()
        {
            { AddressMap.LcdControlRegister, 0x91 },
            { AddressMap.ScrollYRegister, 0x12 },
            { AddressMap.ScrollXRegister, 0x34 },
            { AddressMap.LcdYCompareRegister, 0x56 },
            { AddressMap.BackgroundPaletteRegister, 0xFC },
            { AddressMap.ObjectPalette0Register, 0xA5 },
            { AddressMap.ObjectPalette1Register, 0x5A },
            { AddressMap.WindowYRegister, 0x78 },
            { AddressMap.WindowXRegister, 0x9A },
        };

    [Theory]
    [MemberData(nameof(ReadWriteRegisters))]
    public void WriteRegister_StoresReadWriteRegisters(ushort address, byte value)
    {
        PpuController ppu = CreatePpu();

        ppu.WriteRegister(address, value);

        Assert.Equal(value, ppu.ReadRegister(address));
    }

    [Fact]
    public void ReadRegister_ReturnsStatusReadMaskAndPpuState()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);

        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        Assert.Equal(0x85, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_UpdatesOnlyStatusInterruptSelectBits()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x78);

        Assert.Equal(0xFD, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_IgnoresLcdYCoordinateWrites()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        ppu.WriteRegister(AddressMap.LcdYCoordinateRegister, 0x99);

        Assert.Equal(0x42, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
    }

    [Fact]
    public void Tick_AdvancesVisibleScanlineModes()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(79);
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(1);
        Assert.Equal(0x03, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(172);
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(204);
        Assert.Equal(0x01, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void AccessProperties_ReflectCurrentPpuMode()
    {
        PpuController ppu = CreatePpu();

        AssertVideoRamBlocked(ppu, expected: false);
        AssertObjectAttributeMemoryBlocked(ppu, expected: false);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        AssertVideoRamBlocked(ppu, expected: false);
        AssertObjectAttributeMemoryBlocked(ppu, expected: false);

        ppu.Tick(80);
        AssertVideoRamBlocked(ppu, expected: true);
        AssertObjectAttributeMemoryBlocked(ppu, expected: true);

        ppu.Tick(172);
        AssertVideoRamBlocked(ppu, expected: false);
        AssertObjectAttributeMemoryBlocked(ppu, expected: false);

        ppu.Tick(204);
        AssertVideoRamBlocked(ppu, expected: false);
        AssertObjectAttributeMemoryBlocked(ppu, expected: true);
    }

    [Fact]
    public void Tick_RequestsVBlankInterruptWhenEnteringLineOneHundredFortyFour()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        LcdFrame? frame = ppu.Tick(456 * 144);

        Assert.Equal(144, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x01, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
        Assert.Equal(VBlankInterruptMask, interrupts.InterruptFlag);
        Assert.NotNull(frame);
        Assert.Equal(PpuGeometry.FrameWidth, frame.Width);
        Assert.Equal(PpuGeometry.FrameHeight, frame.Height);
        Assert.Equal(LcdPixelFormat.DmgShadeIndex8, frame.PixelFormat);
        Assert.Equal(PpuGeometry.FrameWidth * PpuGeometry.FrameHeight, frame.Pixels.Length);
    }

    [Fact]
    public void Tick_RequestsModeTwoLcdInterruptWhenEnteringVBlank()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x20);

        ppu.Tick(456 * 144);

        Assert.Equal(
            VBlankInterruptMask | LcdInterruptMask,
            interrupts.InterruptFlag & (VBlankInterruptMask | LcdInterruptMask)
        );
    }

    [Fact]
    public void Tick_RequestsModeTwoLcdInterruptWhenLeavingVBlank()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x20);

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag & LcdInterruptMask);
    }

    [Fact]
    public void Tick_WrapsLyAfterLineOneHundredFiftyThree()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void Tick_DoesNotAdvanceWhenLcdIsDisabled()
    {
        PpuController ppu = CreatePpu();

        LcdFrame? frame = ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
        Assert.Null(frame);
    }

    [Fact]
    public void WriteRegister_DisablingLcdResetsTiming()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.Tick(456 * 3);

        ppu.WriteRegister(AddressMap.LcdControlRegister, 0x00);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void WriteRegister_StatusEnableRequestsLcdInterruptOnRisingStatLine()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x08);

        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_RequestsLcdInterruptOnlyOnStatLineRisingEdge()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x08);

        ppu.Tick(252);
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);

        interrupts.Clear(InterruptSource.Lcd);
        ppu.Tick(10);

        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteRegister_LycCompareUpdatesStatusAndRequestsLcdInterruptOnRisingStatLine()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdYCompareRegister, 0x01);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x40);

        ppu.WriteRegister(AddressMap.LcdYCompareRegister, 0x00);

        Assert.Equal(
            LcdYCompareStatusMask,
            ppu.ReadRegister(AddressMap.LcdStatusRegister) & LcdYCompareStatusMask
        );
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteRegister_RetainsLycCompareWhileLcdIsDisabled()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x84);
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x90);
        ppu.SetRegisterState(AddressMap.LcdYCompareRegister, 0x90);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.WriteRegister(AddressMap.LcdControlRegister, 0x00);
        ppu.WriteRegister(AddressMap.LcdYCompareRegister, 0x01);

        Assert.Equal(0x84, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_EnablingLcdUpdatesLycCompareAndRequestsRisingInterrupt()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x80);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x40);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Equal(
            LcdYCompareStatusMask,
            ppu.ReadRegister(AddressMap.LcdStatusRegister) & LcdYCompareStatusMask
        );
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteRegister_EnablingLcdRequestsModeInterruptWhenLycCompareStaysSet()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x84);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x48);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void LcdFrame_CopiesPixelData()
    {
        byte[] pixels = [0x01, 0x02];

        var frame = new LcdFrame(2, 1, LcdPixelFormat.DmgShadeIndex8, pixels);
        pixels[0] = 0x03;

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_UsesUnsignedBackgroundTileData()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.VideoRam.Write(0x9800, 0x01);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x80, highByte: 0x00);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_UsesSignedBackgroundTileData()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.VideoRam.Write(0x9800, 0x80);
        WriteTileRow(ppu, 0x8800, row: 0, lowByte: 0x00, highByte: 0x80);

        LcdFrame frame = RenderSecondFrame(ppu, LcdEnable | BackgroundEnable);

        Assert.Equal(0x02, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesBackgroundPalette()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, 0x10);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0x00, highByte: 0x80);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesScrollX()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ScrollXRegister, 0x08);
        ppu.VideoRam.Write(0x9801, 0x01);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x80, highByte: 0x80);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x03, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesScrollY()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ScrollYRegister, 0x01);
        WriteTileRow(ppu, 0x8000, row: 1, lowByte: 0x00, highByte: 0x80);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x02, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_FillsVisibleLine()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0xFF, highByte: 0x00);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[PpuGeometry.FrameWidth - 1]);
    }

    [Fact]
    public void Renderer_StartsWindowAtWindowXMinusSeven()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x0F);
        ppu.VideoRam.Write(0x9800, 0x01);
        ppu.VideoRam.Write(0x9C00, 0x02);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8020, row: 0, lowByte: 0x00, highByte: 0xFF);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable
                | BackgroundEnable
                | UnsignedBackgroundTileData
                | WindowEnable
                | WindowTileMap1
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
        Assert.Equal(0x02, frame.Pixels.Span[8]);
    }

    [Fact]
    public void Renderer_IncrementsWindowLineOnlyWhenWindowStarts()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.WindowYRegister, 0x01);
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x07);
        ppu.VideoRam.Write(0x9C00, 0x01);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8010, row: 1, lowByte: 0x00, highByte: 0xFF);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable
                | BackgroundEnable
                | UnsignedBackgroundTileData
                | WindowEnable
                | WindowTileMap1
        );

        Assert.Equal(0x00, frame.Pixels.Span[0]);
        Assert.Equal(0x01, frame.Pixels.Span[PpuGeometry.FrameWidth]);
        Assert.Equal(0x02, frame.Pixels.Span[PpuGeometry.FrameWidth * 2]);
    }

    [Fact]
    public void Renderer_DisablesWindowWhenBackgroundAndWindowDisplayIsDisabled()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x07);
        ppu.VideoRam.Write(0x9C00, 0x02);
        WriteTileRow(ppu, 0x8020, row: 0, lowByte: 0x00, highByte: 0xFF);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | UnsignedBackgroundTileData | WindowEnable | WindowTileMap1
        );

        Assert.Equal(0x00, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Tick_WindowStartupExtendsDrawingMode()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x07);
        ppu.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | WindowEnable
        );
        ppu.Tick(456 * 154);

        ppu.Tick(256);

        Assert.Equal(0x03, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void Renderer_AppliesObjectPalettes()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToDarkGray);
        ppu.WriteRegister(AddressMap.ObjectPalette1Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 16, tile: 1, flags: ObjectPalette1);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x02, frame.Pixels.Span[0]);
        Assert.Equal(0x03, frame.Pixels.Span[8]);
    }

    [Fact]
    public void Renderer_TreatsObjectColorZeroAsTransparent()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0x00, highByte: 0xFF);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x00, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x02, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesObjectFlipFlags()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, IdentityPalette);
        WriteTileRow(ppu, 0x8010, row: 7, lowByte: 0x01, highByte: 0x00);
        WriteObjectAttributes(
            ppu,
            index: 0,
            y: 16,
            x: 8,
            tile: 1,
            flags: ObjectXFlip | ObjectYFlip
        );

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_UsesUnsignedTileDataForLargeObjects()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, IdentityPalette);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x00, highByte: 0xFF);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | ObjectSize16
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
        Assert.Equal(0x02, frame.Pixels.Span[PpuGeometry.FrameWidth * 8]);
    }

    [Fact]
    public void Renderer_GivesObjectPriorityToSmallerXThenOamIndex()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToDarkGray);
        ppu.WriteRegister(AddressMap.ObjectPalette1Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 9, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 8, tile: 1, flags: ObjectPalette1);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x03, frame.Pixels.Span[1]);
    }

    [Fact]
    public void Renderer_ResolvesObjectPriorityBeforeBackgroundPriority()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToDarkGray);
        ppu.WriteRegister(AddressMap.ObjectPalette1Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(
            ppu,
            index: 0,
            y: 16,
            x: 8,
            tile: 1,
            flags: ObjectBehindBackground | ObjectPalette1
        );
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 8, tile: 1, flags: 0);

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_LimitsObjectsPerScanlineBeforeCheckingObjectX()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        for (int index = 0; index < PpuObjectAttributes.MaxObjectsPerScanline; index++)
        {
            WriteObjectAttributes(ppu, index, y: 16, x: 0, tile: 1, flags: 0);
        }

        WriteObjectAttributes(
            ppu,
            PpuObjectAttributes.MaxObjectsPerScanline,
            y: 16,
            x: 8,
            tile: 1,
            flags: 0
        );

        LcdFrame frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x00, frame.Pixels.Span[0]);
    }

    private static PpuController CreatePpu(InterruptController? interrupts = null) =>
        new(interrupts ?? new InterruptController(), new DmgPpuEngine());

    private static LcdFrame RenderSecondFrame(PpuController ppu, byte lcdControl)
    {
        ppu.WriteRegister(AddressMap.LcdControlRegister, lcdControl);
        ppu.Tick(456 * 154);
        return Assert.IsType<LcdFrame>(ppu.Tick(456 * 144));
    }

    private static void WriteTileRow(
        PpuController ppu,
        ushort tileAddress,
        int row,
        byte lowByte,
        byte highByte
    )
    {
        ushort rowAddress = (ushort)(tileAddress + (row * 2));
        ppu.VideoRam.Write(rowAddress, lowByte);
        ppu.VideoRam.Write((ushort)(rowAddress + 1), highByte);
    }

    private static void WriteObjectAttributes(
        PpuController ppu,
        int index,
        byte y,
        byte x,
        byte tile,
        byte flags
    )
    {
        ushort address = (ushort)(
            AddressMap.ObjectAttributeMemoryStart + (index * PpuObjectAttributes.AttributeSize)
        );
        ppu.ObjectAttributeMemory.Write(address, y);
        ppu.ObjectAttributeMemory.Write(
            (ushort)(address + PpuObjectAttributes.XCoordinateOffset),
            x
        );
        ppu.ObjectAttributeMemory.Write(
            (ushort)(address + PpuObjectAttributes.TileIndexOffset),
            tile
        );
        ppu.ObjectAttributeMemory.Write((ushort)(address + PpuObjectAttributes.FlagsOffset), flags);
    }

    private static void AssertVideoRamBlocked(PpuController ppu, bool expected)
    {
        Assert.Equal(expected, ppu.IsCpuVideoRamReadBlocked);
        Assert.Equal(expected, ppu.IsCpuVideoRamWriteBlocked);
    }

    private static void AssertObjectAttributeMemoryBlocked(PpuController ppu, bool expected)
    {
        Assert.Equal(expected, ppu.IsCpuObjectAttributeMemoryReadBlocked);
        Assert.Equal(expected, ppu.IsCpuObjectAttributeMemoryWriteBlocked);
    }
}
