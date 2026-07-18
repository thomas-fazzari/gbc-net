// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
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

    private const ushort CompatibilityBackgroundDarkRgb555 = 0x6180;
    private const ushort CompatibilityObjectDarkRgb555 = 0x1CF2;

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
        var ppu = CreatePpu();

        ppu.WriteRegister(address, value);

        Assert.Equal(value, ppu.ReadRegister(address));
    }

    [Fact]
    public void ReadWriteRegister_SelectsVramBankWhenBanked()
    {
        var ppu = CreatePpu(videoRamBankCount: 2, isVideoRamBankRegisterEnabled: true);

        Assert.Equal(0xFE, ppu.ReadRegister(AddressMap.VideoRamBankRegister));

        ppu.WriteRegister(AddressMap.VideoRamBankRegister, 0xFF);

        Assert.Equal(1, ppu.VideoRam.SelectedBank);
        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.VideoRamBankRegister));

        ppu.WriteRegister(AddressMap.VideoRamBankRegister, 0xFE);

        Assert.Equal(0, ppu.VideoRam.SelectedBank);
        Assert.Equal(0xFE, ppu.ReadRegister(AddressMap.VideoRamBankRegister));
    }

    [Fact]
    public void ReadWriteRegister_IgnoresVramBankRegisterWhenCapabilityDisabled()
    {
        var ppu = CreatePpu(videoRamBankCount: 2);

        ppu.WriteRegister(AddressMap.VideoRamBankRegister, 0x01);

        Assert.Equal(0, ppu.VideoRam.SelectedBank);
        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.VideoRamBankRegister));
    }

    [Fact]
    public void ReadWriteRegister_IgnoresVramBankRegisterWhenUnbanked()
    {
        var ppu = CreatePpu();

        ppu.WriteRegister(AddressMap.VideoRamBankRegister, 0x01);

        Assert.Equal(0, ppu.VideoRam.SelectedBank);
        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.VideoRamBankRegister));
    }

    [Fact]
    public void ReadWriteRegister_StoresColorPaletteIndexWithReadMask()
    {
        var ppu = CreatePpu(isColorPaletteRamEnabled: true);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0xFF);

        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.BackgroundPaletteIndexRegister));

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x40);

        Assert.Equal(0x40, ppu.ReadRegister(AddressMap.BackgroundPaletteIndexRegister));
    }

    [Fact]
    public void ReadWriteRegister_StoresColorPaletteData()
    {
        var ppu = CreatePpu(isColorPaletteRamEnabled: true);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x02);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0xAB);

        Assert.Equal(0xAB, ppu.ReadRegister(AddressMap.BackgroundPaletteDataRegister));
    }

    [Fact]
    public void WriteRegister_AutoIncrementsColorPaletteIndexAfterDataWrite()
    {
        var ppu = CreatePpu(isColorPaletteRamEnabled: true);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x82);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0x11);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0x22);

        Assert.Equal(0xC4, ppu.ReadRegister(AddressMap.BackgroundPaletteIndexRegister));

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x02);

        Assert.Equal(0x11, ppu.ReadRegister(AddressMap.BackgroundPaletteDataRegister));

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x03);

        Assert.Equal(0x22, ppu.ReadRegister(AddressMap.BackgroundPaletteDataRegister));
    }

    [Fact]
    public void ReadRegister_DoesNotAutoIncrementColorPaletteIndexAfterDataRead()
    {
        var ppu = CreatePpu(isColorPaletteRamEnabled: true);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0x11);
        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x80);

        Assert.Equal(0x11, ppu.ReadRegister(AddressMap.BackgroundPaletteDataRegister));
        Assert.Equal(0xC0, ppu.ReadRegister(AddressMap.BackgroundPaletteIndexRegister));
    }

    [Fact]
    public void WriteRegister_WrapsColorPaletteAutoIncrement()
    {
        var ppu = CreatePpu(isColorPaletteRamEnabled: true);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0xBF);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0x33);

        Assert.Equal(0xC0, ppu.ReadRegister(AddressMap.BackgroundPaletteIndexRegister));
    }

    [Fact]
    public void ReadWriteRegister_StoresBackgroundAndObjectColorPalettesIndependently()
    {
        var ppu = CreatePpu(isColorPaletteRamEnabled: true);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x01);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0x12);
        ppu.WriteRegister(AddressMap.ObjectPaletteIndexRegister, 0x01);
        ppu.WriteRegister(AddressMap.ObjectPaletteDataRegister, 0x34);

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x01);
        ppu.WriteRegister(AddressMap.ObjectPaletteIndexRegister, 0x01);

        Assert.Equal(0x12, ppu.ReadRegister(AddressMap.BackgroundPaletteDataRegister));
        Assert.Equal(0x34, ppu.ReadRegister(AddressMap.ObjectPaletteDataRegister));
    }

    [Fact]
    public void ReadWriteRegister_IgnoresColorPaletteRegistersWhenDisabled()
    {
        var ppu = CreatePpu();

        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0x81);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0x12);
        ppu.WriteRegister(AddressMap.ObjectPaletteIndexRegister, 0x82);
        ppu.WriteRegister(AddressMap.ObjectPaletteDataRegister, 0x34);

        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.BackgroundPaletteIndexRegister));
        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.BackgroundPaletteDataRegister));
        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.ObjectPaletteIndexRegister));
        Assert.Equal(0xFF, ppu.ReadRegister(AddressMap.ObjectPaletteDataRegister));
    }

    [Fact]
    public void ReadRegister_ReturnsStatusReadMaskAndPpuState()
    {
        var ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);

        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        Assert.Equal(0x85, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_UpdatesOnlyStatusInterruptSelectBits()
    {
        var ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x78);

        Assert.Equal(0xFD, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_IgnoresLcdYCoordinateWrites()
    {
        var ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        ppu.WriteRegister(AddressMap.LcdYCoordinateRegister, 0x99);

        Assert.Equal(0x42, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
    }

    [Fact]
    public void Tick_AdvancesVisibleScanlineModes()
    {
        var ppu = CreatePpu();
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
        var ppu = CreatePpu();

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
        var ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        var frame = ppu.Tick(456 * 144).CompletedFrame;

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
    public void Tick_SkipsCompletedFrameWhenVideoRenderingIsDisabled()
    {
        var interrupts = new InterruptController();
        var ppu = CreatePpu(interrupts);
        ppu.VideoRenderingEnabled = false;
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        var frame = ppu.Tick(456 * 144).CompletedFrame;

        Assert.Null(frame);
        Assert.Equal(144, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(VBlankInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_CapturesFrameAfterVideoRenderingIsEnabledAgain()
    {
        var ppu = CreatePpu();
        ppu.VideoRenderingEnabled = false;
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Null(ppu.Tick(456 * 144).CompletedFrame);

        ppu.VideoRenderingEnabled = true;
        ppu.Tick(456 * 10);

        var frame = ppu.Tick(456 * 144).CompletedFrame;

        Assert.NotNull(frame);
        Assert.Equal(PpuGeometry.FrameWidth, frame.Width);
        Assert.Equal(PpuGeometry.FrameHeight, frame.Height);
    }

    [Fact]
    public void Tick_RequestsModeTwoLcdInterruptWhenEnteringVBlank()
    {
        var interrupts = new InterruptController();
        var ppu = CreatePpu(interrupts);
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
        var ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x20);

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag & LcdInterruptMask);
    }

    [Fact]
    public void Tick_WrapsLyAfterLineOneHundredFiftyThree()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void Tick_DoesNotAdvanceWhenLcdIsDisabled()
    {
        var ppu = CreatePpu();

        var frame = ppu.Tick(456 * 154).CompletedFrame;

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
        Assert.Null(frame);
    }

    [Fact]
    public void WriteRegister_DisablingLcdResetsTiming()
    {
        var ppu = CreatePpu();
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
        var ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x08);

        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_RequestsLcdInterruptOnlyOnStatLineRisingEdge()
    {
        var interrupts = new InterruptController();
        var ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x08);

        ppu.Tick(252);
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);

        interrupts.Clear(InterruptSource.LcdStat);
        ppu.Tick(10);

        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteRegister_LycCompareUpdatesStatusAndRequestsLcdInterruptOnRisingStatLine()
    {
        var interrupts = new InterruptController();
        var ppu = CreatePpu(interrupts);
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
        var ppu = CreatePpu();
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
        var ppu = CreatePpu(interrupts);
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
        var ppu = CreatePpu(interrupts);
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
    public void LcdFrame_FromOwnedPixelsUsesProvidedBuffer()
    {
        byte[] pixels = [0x01, 0x02];

        var frame = LcdFrame.FromOwnedPixels(1, 1, LcdPixelFormat.Rgb555Le, pixels);

        Assert.True(MemoryMarshal.TryGetArray(frame.Pixels, out var segment));
        Assert.Same(pixels, segment.Array);
        Assert.Equal(0, segment.Offset);
        Assert.Equal(pixels.Length, segment.Count);
    }

    [Fact]
    public void Renderer_UsesUnsignedBackgroundTileData()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.VideoRam.Write(0x9800, 0x01);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x80, highByte: 0x00);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_UsesSignedBackgroundTileData()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.VideoRam.Write(0x9800, 0x80);
        WriteTileRow(ppu, 0x8800, row: 0, lowByte: 0x00, highByte: 0x80);

        var frame = RenderSecondFrame(ppu, LcdEnable | BackgroundEnable);

        Assert.Equal(0x02, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesBackgroundPalette()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, 0x10);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0x00, highByte: 0x80);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesScrollX()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ScrollXRegister, 0x08);
        ppu.VideoRam.Write(0x9801, 0x01);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x80, highByte: 0x80);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x03, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesScrollY()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ScrollYRegister, 0x01);
        WriteTileRow(ppu, 0x8000, row: 1, lowByte: 0x00, highByte: 0x80);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x02, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_FillsVisibleLine()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0xFF, highByte: 0x00);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[PpuGeometry.FrameWidth - 1]);
    }

    [Fact]
    public void Renderer_StartsWindowAtWindowXMinusSeven()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x0F);
        ppu.VideoRam.Write(0x9800, 0x01);
        ppu.VideoRam.Write(0x9C00, 0x02);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8020, row: 0, lowByte: 0x00, highByte: 0xFF);

        var frame = RenderSecondFrame(
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
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.WindowYRegister, 0x01);
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x07);
        ppu.VideoRam.Write(0x9C00, 0x01);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8010, row: 1, lowByte: 0x00, highByte: 0xFF);

        var frame = RenderSecondFrame(
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
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.WindowXRegister, 0x07);
        ppu.VideoRam.Write(0x9C00, 0x02);
        WriteTileRow(ppu, 0x8020, row: 0, lowByte: 0x00, highByte: 0xFF);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | UnsignedBackgroundTileData | WindowEnable | WindowTileMap1
        );

        Assert.Equal(0x00, frame.Pixels.Span[0]);
    }

    [Fact]
    public void CgbRenderer_CompletesRgb555Frame()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        Assert.Equal(PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * 2, frame.Pixels.Length);
    }

    [Fact]
    public void CgbRenderer_UsesBackgroundPaletteSelectedByTileAttribute()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteBackgroundColor(ppu, paletteIndex: 5, colorId: 2, rgb555: 0x1234);
        WriteVideoRamBank(ppu, bank: 1, PpuTileData.TileMap0Start, value: 0x05);
        WriteBankedTileRow(ppu, bank: 0, 0x8000, row: 0, lowByte: 0x00, highByte: 0x80);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x1234);
    }

    [Fact]
    public void CgbRenderer_UsesTileDataBankSelectedByTileAttribute()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteBackgroundColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x001F);
        WriteBackgroundColor(ppu, paletteIndex: 0, colorId: 3, rgb555: 0x03E0);
        WriteVideoRamBank(ppu, bank: 0, PpuTileData.TileMap0Start, value: 0x01);
        WriteVideoRamBank(ppu, bank: 1, PpuTileData.TileMap0Start, value: 0x08);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0x80, highByte: 0x00);
        WriteBankedTileRow(ppu, bank: 1, 0x8010, row: 0, lowByte: 0x80, highByte: 0x80);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x03E0);
    }

    [Fact]
    public void CgbRenderer_AppliesTileAttributeXFlip()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteBackgroundColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x001F);
        WriteVideoRamBank(ppu, bank: 1, PpuTileData.TileMap0Start, value: 0x20);
        WriteBankedTileRow(ppu, bank: 0, 0x8000, row: 0, lowByte: 0x01, highByte: 0x00);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x001F);
    }

    [Fact]
    public void CgbRenderer_AppliesTileAttributeYFlip()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteBackgroundColor(ppu, paletteIndex: 0, colorId: 2, rgb555: 0x7C00);
        WriteVideoRamBank(ppu, bank: 1, PpuTileData.TileMap0Start, value: 0x40);
        WriteBankedTileRow(ppu, bank: 0, 0x8000, row: 7, lowByte: 0x00, highByte: 0x80);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x7C00);
    }

    [Fact]
    public void CgbRenderer_UsesObjectPaletteSelectedByOamAttribute()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteObjectColor(ppu, paletteIndex: 5, colorId: 1, rgb555: 0x1234);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0x80, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0x05);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x1234);
    }

    [Fact]
    public void CgbRenderer_UsesObjectTileDataBankSelectedByOamAttribute()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteObjectColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x001F);
        WriteObjectColor(ppu, paletteIndex: 0, colorId: 3, rgb555: 0x03E0);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0x80, highByte: 0x00);
        WriteBankedTileRow(ppu, bank: 1, 0x8010, row: 0, lowByte: 0x80, highByte: 0x80);
        WriteObjectAttributes(
            ppu,
            index: 0,
            y: 16,
            x: 8,
            tile: 1,
            flags: PpuObjectAttributes.CgbTileBankMask
        );

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x03E0);
    }

    [Fact]
    public void CgbRenderer_AppliesObjectFlipFlags()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteObjectColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x7C00);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 7, lowByte: 0x01, highByte: 0x00);
        WriteObjectAttributes(
            ppu,
            index: 0,
            y: 16,
            x: 8,
            tile: 1,
            flags: ObjectXFlip | ObjectYFlip
        );

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x7C00);
    }

    [Fact]
    public void CgbRenderer_TreatsObjectColorZeroAsTransparent()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteBackgroundColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x1234);
        WriteBankedTileRow(ppu, bank: 0, 0x8000, row: 0, lowByte: 0x80, highByte: 0x00);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0x00, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x1234);
    }

    [Fact]
    public void CgbRenderer_LetsNonZeroBackgroundCoverPriorityObject()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteBackgroundColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x7C00);
        WriteObjectColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x001F);
        WriteBankedTileRow(ppu, bank: 0, 0x8000, row: 0, lowByte: 0x80, highByte: 0x00);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0x80, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: ObjectBehindBackground);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x7C00);
    }

    [Fact]
    public void CgbRenderer_UsesOamObjectPriorityByDefault()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteObjectColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x001F);
        WriteObjectColor(ppu, paletteIndex: 1, colorId: 1, rgb555: 0x03E0);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 14, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 8, tile: 1, flags: 1);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 6, expected: 0x001F);
    }

    [Fact]
    public void CgbRenderer_UsesXCoordinateObjectPriorityWhenOpriSelectsDmgMode()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        ppu.WriteRegister(AddressMap.ObjectPriorityModeRegister, 0x01);
        WriteObjectColor(ppu, paletteIndex: 0, colorId: 1, rgb555: 0x001F);
        WriteObjectColor(ppu, paletteIndex: 1, colorId: 1, rgb555: 0x03E0);
        WriteBankedTileRow(ppu, bank: 0, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 14, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 8, tile: 1, flags: 1);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 6, expected: 0x03E0);
    }

    [Fact]
    public void CgbDmgCompatibilityRenderer_CompletesRgb555Frame()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));
        ppu.SetDmgCompatibilityColorPaletteRam(CgbCompatibilityPaletteSelector.Default.Palettes);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        Assert.Equal(PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * 2, frame.Pixels.Length);
    }

    [Fact]
    public void CgbDmgCompatibilityRenderer_MapsBackgroundPaletteThroughCgbPalette0()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));
        ppu.SetDmgCompatibilityColorPaletteRam(CgbCompatibilityPaletteSelector.Default.Palettes);
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, PaletteColorOneToDarkGray);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0x80, highByte: 0x00);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );

        Rgb555Assertions.PixelEquals(
            frame,
            pixelIndex: 0,
            expected: CompatibilityBackgroundDarkRgb555
        );
    }

    [Fact]
    public void CgbDmgCompatibilityRenderer_MapsObjectPalettesThroughCgbObjectPalettes()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));
        ppu.SetDmgCompatibilityColorPaletteRam(CgbCompatibilityPaletteSelector.Default.Palettes);
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToDarkGray);
        ppu.WriteRegister(AddressMap.ObjectPalette1Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 16, tile: 1, flags: ObjectPalette1);

        var frame = RenderSecondFrame(ppu, LcdEnable | ObjectEnable | UnsignedBackgroundTileData);

        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: CompatibilityObjectDarkRgb555);
        Rgb555Assertions.PixelEquals(frame, pixelIndex: 8, expected: 0x0000);
    }

    [Fact]
    public void Tick_WindowStartupExtendsDrawingMode()
    {
        var ppu = CreatePpu();
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
    public void Tick_CgbObjectPenaltyExtendsDrawingMode()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 0, flags: 0);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable | ObjectEnable);

        ppu.Tick(252);
        Assert.Equal(0x03, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(8);
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void Tick_DisabledVideoRenderingPreservesWindowTiming()
    {
        var ppu = CreatePpu();
        ppu.VideoRenderingEnabled = false;
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
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToDarkGray);
        ppu.WriteRegister(AddressMap.ObjectPalette1Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 16, tile: 1, flags: ObjectPalette1);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x02, frame.Pixels.Span[0]);
        Assert.Equal(0x03, frame.Pixels.Span[8]);
    }

    [Fact]
    public void Renderer_TreatsObjectColorZeroAsTransparent()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, IdentityPalette);
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0x00, highByte: 0xFF);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x00, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x02, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_AppliesObjectFlipFlags()
    {
        var ppu = CreatePpu();
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

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_UsesUnsignedTileDataForLargeObjects()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, IdentityPalette);
        WriteTileRow(ppu, 0x8000, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0x00, highByte: 0xFF);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 8, tile: 1, flags: 0);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | ObjectSize16
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
        Assert.Equal(0x02, frame.Pixels.Span[PpuGeometry.FrameWidth * 8]);
    }

    [Fact]
    public void Renderer_GivesObjectPriorityToSmallerXThenOamIndex()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToDarkGray);
        ppu.WriteRegister(AddressMap.ObjectPalette1Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        WriteObjectAttributes(ppu, index: 0, y: 16, x: 9, tile: 1, flags: 0);
        WriteObjectAttributes(ppu, index: 1, y: 16, x: 8, tile: 1, flags: ObjectPalette1);

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x03, frame.Pixels.Span[1]);
    }

    [Fact]
    public void Renderer_ResolvesObjectPriorityBeforeBackgroundPriority()
    {
        var ppu = CreatePpu();
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

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x01, frame.Pixels.Span[0]);
    }

    [Fact]
    public void Renderer_LimitsObjectsPerScanlineBeforeCheckingObjectX()
    {
        var ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.ObjectPalette0Register, PaletteColorOneToBlack);
        WriteTileRow(ppu, 0x8010, row: 0, lowByte: 0xFF, highByte: 0x00);
        for (var index = 0; index < PpuObjectAttributes.MaxObjectsPerScanline; index++)
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

        var frame = RenderSecondFrame(
            ppu,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );

        Assert.Equal(0x00, frame.Pixels.Span[0]);
    }

    private static PpuController CreatePpu(
        InterruptController? interrupts = null,
        int videoRamBankCount = 1,
        bool isVideoRamBankRegisterEnabled = false,
        bool isColorPaletteIndexRegisterEnabled = false,
        bool isColorPaletteRamEnabled = false
    ) =>
        new(
            interrupts ?? new InterruptController(),
            new DmgPpuEngine(),
            videoRamBankCount,
            isVideoRamBankRegisterEnabled,
            isColorPaletteIndexRegisterEnabled || isColorPaletteRamEnabled,
            isColorPaletteRamEnabled,
            isObjectPriorityModeRegisterEnabled: false
        );

    private static PpuController CreatePpu(CgbHardwareProfile profile) =>
        new(
            new InterruptController(),
            profile.CreatePpuEngine(),
            profile.VideoRamBankCount,
            profile.IsVideoRamBankRegisterEnabled,
            profile.IsColorPaletteIndexRegisterEnabled,
            profile.IsColorPaletteRamEnabled,
            profile.IsObjectPriorityModeRegisterEnabled
        );

    private static LcdFrame RenderSecondFrame(PpuController ppu, byte lcdControl)
    {
        ppu.WriteRegister(AddressMap.LcdControlRegister, lcdControl);
        ppu.Tick(456 * 154);
        return Assert.IsType<LcdFrame>(ppu.Tick(456 * 144).CompletedFrame);
    }

    private static void WriteTileRow(
        PpuController ppu,
        ushort tileAddress,
        int row,
        byte lowByte,
        byte highByte
    )
    {
        var rowAddress = (ushort)(tileAddress + (row * 2));
        ppu.VideoRam.Write(rowAddress, lowByte);
        ppu.VideoRam.Write((ushort)(rowAddress + 1), highByte);
    }

    private static void WriteBankedTileRow(
        PpuController ppu,
        int bank,
        ushort tileAddress,
        int row,
        byte lowByte,
        byte highByte
    )
    {
        var rowAddress = (ushort)(tileAddress + (row * 2));
        WriteVideoRamBank(ppu, bank, rowAddress, lowByte);
        WriteVideoRamBank(ppu, bank, (ushort)(rowAddress + 1), highByte);
    }

    private static void WriteVideoRamBank(PpuController ppu, int bank, ushort address, byte value)
    {
        ppu.WriteRegister(AddressMap.VideoRamBankRegister, (byte)bank);
        ppu.VideoRam.Write(address, value);
        ppu.WriteRegister(AddressMap.VideoRamBankRegister, 0);
    }

    private static void WriteBackgroundColor(
        PpuController ppu,
        int paletteIndex,
        byte colorId,
        ushort rgb555
    )
    {
        var offset = (byte)((((paletteIndex & 0x07) * 4) + (colorId & 0x03)) * 2);
        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, offset);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, (byte)rgb555);
        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, (byte)(offset + 1));
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, (byte)(rgb555 >> 8));
    }

    private static void WriteObjectColor(
        PpuController ppu,
        int paletteIndex,
        byte colorId,
        ushort rgb555
    )
    {
        var offset = (byte)((((paletteIndex & 0x07) * 4) + (colorId & 0x03)) * 2);
        ppu.WriteRegister(AddressMap.ObjectPaletteIndexRegister, offset);
        ppu.WriteRegister(AddressMap.ObjectPaletteDataRegister, (byte)rgb555);
        ppu.WriteRegister(AddressMap.ObjectPaletteIndexRegister, (byte)(offset + 1));
        ppu.WriteRegister(AddressMap.ObjectPaletteDataRegister, (byte)(rgb555 >> 8));
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
        var address = (ushort)(
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
