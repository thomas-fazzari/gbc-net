// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Engines;

namespace GbcNet.Tests.Ppu;

public sealed class PpuStateTests
{
    private const byte LcdEnable = 0x80;
    private const byte BackgroundEnable = 0x01;
    private const byte ObjectEnable = 0x02;
    private const byte UnsignedBackgroundTileData = 0x10;
    private const byte WindowEnable = 0x20;

    public static TheoryData<int> Profiles => [0, 1, 2];

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_ImmediatelyAfterBoot_RoundTripsEveryProfile(int profileIndex)
    {
        var profile = CreateProfile(profileIndex);
        var source = CreatePpu(profile, out _);
        var destination = CreatePpu(profile, out _);
        var state = source.CaptureState();

        destination.RestoreState(state);

        AssertControllerStateEqual(state, destination.CaptureState());
    }

    [Fact]
    public void CaptureState_DefensivelyOwnsEveryMutablePpuBuffer()
    {
        var ppu = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb), out _);
        ConfigureBackground(ppu);
        WriteObject(ppu, 0, y: 16, x: 16, tile: 0, flags: 0);
        ppu.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );
        ppu.Tick(100);

        var captured = ppu.CaptureState();
        var cgb = Assert.IsType<CgbPpuEngineState>(captured.Engine);
        var expectedVideoRam = captured.VideoRam.Banks.ToArray();
        var expectedBackgroundPalette = captured.BackgroundPaletteRam.Bytes.ToArray();
        var expectedObjectPalette = captured.ObjectPaletteRam.Bytes.ToArray();
        var expectedOam = captured.ObjectAttributeMemory.Bytes.ToArray();
        var expectedFrameBuffer = cgb.Common.FrameBuffer.ToArray();
        var expectedBackgroundColors = cgb.BackgroundColorFifo.ToArray();
        var expectedBackgroundAttributes = cgb.BackgroundAttributeFifo.ToArray();
        var expectedObjects = cgb.Objects.Selector.Objects.ToArray();

        captured.VideoRam.Banks[0] ^= 0xFF;
        captured.BackgroundPaletteRam.Bytes[0] ^= 0xFF;
        captured.ObjectPaletteRam.Bytes[0] ^= 0xFF;
        captured.ObjectAttributeMemory.Bytes[0] ^= 0xFF;
        cgb.Common.FrameBuffer[0] ^= 0xFF;
        cgb.BackgroundColorFifo[0] ^= 0xFF;
        cgb.BackgroundAttributeFifo[0] ^= 0xFF;
        cgb.Objects.Selector.Objects[0] = default;

        var recaptured = Assert.IsType<CgbPpuEngineState>(ppu.CaptureState().Engine);
        Assert.Equal(expectedVideoRam, ppu.CaptureState().VideoRam.Banks);
        Assert.Equal(expectedBackgroundPalette, ppu.CaptureState().BackgroundPaletteRam.Bytes);
        Assert.Equal(expectedObjectPalette, ppu.CaptureState().ObjectPaletteRam.Bytes);
        Assert.Equal(expectedOam, ppu.CaptureState().ObjectAttributeMemory.Bytes);
        Assert.Equal(expectedFrameBuffer, recaptured.Common.FrameBuffer);
        Assert.Equal(expectedBackgroundColors, recaptured.BackgroundColorFifo);
        Assert.Equal(expectedBackgroundAttributes, recaptured.BackgroundAttributeFifo);
        Assert.Equal(expectedObjects, recaptured.Objects.Selector.Objects);
    }

    [Fact]
    public void RestoreState_RejectsMalformedLateNestedPayloadAndWrongEngineAtomically()
    {
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var source = CreatePpu(profile, out _);
        ConfigureBackground(source);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);

        var destination = CreatePpu(profile, out _);
        destination.VideoRam.Write(AddressMap.VideoRamStart, 0xAA);
        destination.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, 0);
        destination.WriteRegister(AddressMap.BackgroundPaletteDataRegister, 0xBB);
        destination.ObjectAttributeMemory.Write(AddressMap.ObjectAttributeMemoryStart, 0xCC);
        var before = destination.CaptureState();

        var malformed = source.CaptureState() with
        {
            ObjectAttributeMemory = new MappedMemoryState(new byte[1]),
        };

        Assert.Throws<ArgumentException>(() => destination.RestoreState(malformed));
        AssertControllerStateEqual(before, destination.CaptureState());

        var wrongEngine = source.CaptureState() with { Engine = new DmgPpuEngine().CaptureState() };

        Assert.Throws<ArgumentException>(() => destination.RestoreState(wrongEngine));
        AssertControllerStateEqual(before, destination.CaptureState());
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_MidBackgroundFifoAndPartialFramebuffer_ContinuesIdentically(
        int profileIndex
    )
    {
        var profile = CreateProfile(profileIndex);
        var source = CreatePpu(profile, out var sourceInterrupts);
        ConfigureBackground(source);
        source.WriteRegister(AddressMap.LcdStatusRegister, 0x78);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);
        sourceInterrupts.SetInterruptFlag(0);

        var state = source.CaptureState();
        Assert.True(GetCommon(state.Engine).RenderedPixels > 0);
        Assert.True(GetCommon(state.Engine).BackgroundWindowFetcher.BackgroundFifoCount > 0);

        var restored = CreatePpu(profile, out var restoredInterrupts);
        restored.RestoreState(state);

        Assert.Equal(0, restoredInterrupts.InterruptFlag);
        Assert.Null(restored.Tick(0).CompletedFrame);
        DriveIdenticallyToCompletedFrame(source, sourceInterrupts, restored, restoredInterrupts);
    }

    [Fact]
    public void RestoreState_CgbTileAttributesRemainObservable()
    {
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var source = CreatePpu(profile, out var sourceInterrupts);
        WriteBackgroundColor(source, paletteIndex: 2, colorId: 1, rgb555: 0x1234);
        WriteTileRow(
            source,
            tileAddress: AddressMap.VideoRamStart,
            row: 0,
            lowByte: 0x80,
            highByte: 0x00
        );
        source.VideoRam.WriteBank(1, 0x9800, 0x02);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);
        sourceInterrupts.SetInterruptFlag(0);

        var restored = CreatePpu(profile, out var restoredInterrupts);
        restored.RestoreState(source.CaptureState());

        var frame = DriveIdenticallyToCompletedFrame(
            source,
            sourceInterrupts,
            restored,
            restoredInterrupts
        );
        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        Assert.Equal(0x34, frame.Pixels.Span[0]);
        Assert.Equal(0x12, frame.Pixels.Span[1]);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_MidWindowFetch_ContinuesIdentically(int profileIndex)
    {
        var profile = CreateProfile(profileIndex);
        var source = CreatePpu(profile, out var sourceInterrupts);
        ConfigureBackground(source);
        source.WriteRegister(AddressMap.WindowYRegister, 0);
        source.WriteRegister(AddressMap.WindowXRegister, 7);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | WindowEnable | UnsignedBackgroundTileData
        );
        source.Tick(110);
        sourceInterrupts.SetInterruptFlag(0);

        var state = source.CaptureState();
        Assert.True(GetCommon(state.Engine).BackgroundWindowFetcher.WindowActiveThisLine);
        var restored = CreatePpu(profile, out var restoredInterrupts);
        restored.RestoreState(state);

        DriveIdenticallyToCompletedFrame(source, sourceInterrupts, restored, restoredInterrupts);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_AfterObjectSelection_ContinuesIdentically(int profileIndex)
    {
        var profile = CreateProfile(profileIndex);
        var source = CreatePpu(profile, out var sourceInterrupts);
        ConfigureBackground(source);
        WriteObject(source, 0, y: 16, x: 16, tile: 0, flags: 0);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);
        sourceInterrupts.SetInterruptFlag(0);

        var state = source.CaptureState();
        Assert.True(GetObjects(state.Engine).Selected);
        var restored = CreatePpu(profile, out var restoredInterrupts);
        restored.RestoreState(state);

        DriveIdenticallyToCompletedFrame(source, sourceInterrupts, restored, restoredInterrupts);
    }

    [Fact]
    public void ValidateState_RejectsInvalidStatusAndObjectPriorityMode()
    {
        var cgb = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb), out _);
        var cgbState = cgb.CaptureState();

        Assert.Throws<ArgumentException>(() =>
            cgb.ValidateState(cgbState with { StatusInterruptSelect = 0x80 })
        );
        Assert.Throws<ArgumentException>(() =>
            cgb.ValidateState(cgbState with { ObjectPriorityMode = (ObjectPriorityMode)2 })
        );

        var dmg = CreatePpu(DmgHardwareProfile.Instance, out _);
        Assert.Throws<ArgumentException>(() =>
            dmg.ValidateState(
                dmg.CaptureState() with
                {
                    ObjectPriorityMode = ObjectPriorityMode.LowerXWins,
                }
            )
        );
    }

    [Fact]
    public void RestoreState_PreservesCurrentFrameRenderLatchWhenHostRenderingChanges()
    {
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var source = CreatePpu(profile, out var sourceInterrupts);
        ConfigureBackground(source);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);
        sourceInterrupts.SetInterruptFlag(0);
        var state = source.CaptureState() with { VideoRenderingEnabled = false };
        source.VideoRenderingEnabled = false;

        var restored = CreatePpu(profile, out var restoredInterrupts);
        restored.RestoreState(state);

        Assert.False(restored.VideoRenderingEnabled);
        var frame = DriveIdenticallyToCompletedFrame(
            source,
            sourceInterrupts,
            restored,
            restoredInterrupts
        );
        Assert.NotEmpty(frame.Pixels.Span.ToArray());
    }

    private static IHardwareProfile CreateProfile(int profileIndex) =>
        profileIndex switch
        {
            0 => DmgHardwareProfile.Instance,
            1 => new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility),
            2 => new CgbHardwareProfile(CgbOperatingMode.Cgb),
            _ => throw new ArgumentOutOfRangeException(nameof(profileIndex)),
        };

    private static PpuController CreatePpu(
        IHardwareProfile profile,
        out InterruptController interrupts
    )
    {
        interrupts = new InterruptController();
        return new PpuController(
            interrupts,
            profile.CreatePpuEngine(),
            profile.VideoRamBankCount,
            profile.IsVideoRamBankRegisterEnabled,
            profile.IsColorPaletteIndexRegisterEnabled,
            profile.IsColorPaletteRamEnabled,
            profile.IsObjectPriorityModeRegisterEnabled
        );
    }

    private static void ConfigureBackground(PpuController ppu)
    {
        WriteTileRow(ppu, AddressMap.VideoRamStart, 0, lowByte: 0xAA, highByte: 0x55);
        ppu.VideoRam.Write(0x9800, 0);
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

    private static void WriteObject(
        PpuController ppu,
        int index,
        byte y,
        byte x,
        byte tile,
        byte flags
    )
    {
        var address = (ushort)(AddressMap.ObjectAttributeMemoryStart + (index * 4));
        ppu.ObjectAttributeMemory.Write(address, y);
        ppu.ObjectAttributeMemory.Write((ushort)(address + 1), x);
        ppu.ObjectAttributeMemory.Write((ushort)(address + 2), tile);
        ppu.ObjectAttributeMemory.Write((ushort)(address + 3), flags);
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

    private static LcdFrame DriveIdenticallyToCompletedFrame(
        PpuController source,
        InterruptController sourceInterrupts,
        PpuController restored,
        InterruptController restoredInterrupts
    )
    {
        for (var elapsed = 0; elapsed < 456 * 154; elapsed += 17)
        {
            var dots = Math.Min(17, (456 * 154) - elapsed);
            var sourceResult = source.Tick(dots);
            var restoredResult = restored.Tick(dots);

            Assert.Equal(sourceResult.Interrupts, restoredResult.Interrupts);
            Assert.Equal(sourceResult.EnteredVisibleHBlank, restoredResult.EnteredVisibleHBlank);
            Assert.Equal(sourceInterrupts.InterruptFlag, restoredInterrupts.InterruptFlag);
            Assert.Equal(
                source.ReadRegister(AddressMap.LcdYCoordinateRegister),
                restored.ReadRegister(AddressMap.LcdYCoordinateRegister)
            );
            Assert.Equal(
                source.ReadRegister(AddressMap.LcdStatusRegister),
                restored.ReadRegister(AddressMap.LcdStatusRegister)
            );
            Assert.Equal(source.IsCpuVideoRamReadBlocked, restored.IsCpuVideoRamReadBlocked);
            Assert.Equal(
                source.IsCpuObjectAttributeMemoryReadBlocked,
                restored.IsCpuObjectAttributeMemoryReadBlocked
            );
            Assert.Equal(
                sourceResult.CompletedFrame is null,
                restoredResult.CompletedFrame is null
            );

            if (
                sourceResult.CompletedFrame is { } sourceFrame
                && restoredResult.CompletedFrame is { } restoredFrame
            )
            {
                Assert.Equal(sourceFrame.PixelFormat, restoredFrame.PixelFormat);
                Assert.Equal(sourceFrame.Pixels.ToArray(), restoredFrame.Pixels.ToArray());
                return restoredFrame;
            }
        }

        throw new InvalidOperationException("PPU did not complete a frame.");
    }

    private static PpuEngineBaseState GetCommon(IPpuEngineState state) =>
        state switch
        {
            DmgPpuEngineState dmg => dmg.PixelRules.Common,
            CgbDmgCompatibilityPpuEngineState compatibility => compatibility.PixelRules.Common,
            CgbPpuEngineState cgb => cgb.Common,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

    private static ScanlineObjectSelectorState GetObjects(IPpuEngineState state) =>
        state switch
        {
            DmgPpuEngineState dmg => dmg.PixelRules.Objects.Selector,
            CgbDmgCompatibilityPpuEngineState compatibility => compatibility
                .PixelRules
                .Objects
                .Selector,
            CgbPpuEngineState cgb => cgb.Objects.Selector,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

    private static void AssertControllerStateEqual(
        PpuControllerState expected,
        PpuControllerState actual
    )
    {
        Assert.Equal(expected.VideoRam.Banks, actual.VideoRam.Banks);
        Assert.Equal(expected.VideoRam.SelectedBank, actual.VideoRam.SelectedBank);
        Assert.Equal(expected.BackgroundPaletteRam.Bytes, actual.BackgroundPaletteRam.Bytes);
        Assert.Equal(expected.BackgroundPaletteRam.Index, actual.BackgroundPaletteRam.Index);
        Assert.Equal(expected.ObjectPaletteRam.Bytes, actual.ObjectPaletteRam.Bytes);
        Assert.Equal(expected.ObjectPaletteRam.Index, actual.ObjectPaletteRam.Index);
        Assert.Equal(expected.ObjectAttributeMemory.Bytes, actual.ObjectAttributeMemory.Bytes);
        Assert.Equal(expected.Control, actual.Control);
        Assert.Equal(expected.StatusInterruptSelect, actual.StatusInterruptSelect);
        Assert.Equal(expected.ScrollY, actual.ScrollY);
        Assert.Equal(expected.ScrollX, actual.ScrollX);
        Assert.Equal(expected.LcdYCompare, actual.LcdYCompare);
        Assert.Equal(expected.BackgroundPalette, actual.BackgroundPalette);
        Assert.Equal(expected.ObjectPalette0, actual.ObjectPalette0);
        Assert.Equal(expected.ObjectPalette1, actual.ObjectPalette1);
        Assert.Equal(expected.WindowY, actual.WindowY);
        Assert.Equal(expected.WindowX, actual.WindowX);
        Assert.Equal(expected.ObjectPriorityMode, actual.ObjectPriorityMode);
        Assert.Equal(expected.VideoRenderingEnabled, actual.VideoRenderingEnabled);
        Assert.Equal(expected.Engine.GetType(), actual.Engine.GetType());
        Assert.Equal(GetCommon(expected.Engine).Timing, GetCommon(actual.Engine).Timing);
        Assert.Equal(
            GetCommon(expected.Engine).StatInterruptLatch,
            GetCommon(actual.Engine).StatInterruptLatch
        );
        Assert.Equal(
            GetCommon(expected.Engine).BackgroundWindowFetcher,
            GetCommon(actual.Engine).BackgroundWindowFetcher
        );
        Assert.Equal(GetCommon(expected.Engine).FrameBuffer, GetCommon(actual.Engine).FrameBuffer);
        Assert.Equal(
            GetCommon(expected.Engine).RenderedPixels,
            GetCommon(actual.Engine).RenderedPixels
        );
        Assert.Equal(
            GetCommon(expected.Engine).RenderingScanline,
            GetCommon(actual.Engine).RenderingScanline
        );
        Assert.Equal(
            GetCommon(expected.Engine).RenderCurrentFrame,
            GetCommon(actual.Engine).RenderCurrentFrame
        );
    }
}
