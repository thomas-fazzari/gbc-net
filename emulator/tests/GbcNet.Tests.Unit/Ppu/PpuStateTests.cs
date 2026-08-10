// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Engines;
using static GbcNet.Tests.Unit.Ppu.PpuTestHelpers;

namespace GbcNet.Tests.Unit.Ppu;

public sealed class PpuStateTests
{
    public enum PpuTestProfile
    {
        Dmg = 0,
        CgbDmgCompatibility = 1,
        Cgb = 2,
    }

    private const byte LcdEnable = 0x80;
    private const byte BackgroundEnable = 0x01;
    private const byte ObjectEnable = 0x02;
    private const byte UnsignedBackgroundTileData = 0x10;
    private const byte WindowEnable = 0x20;

    public static TheoryData<PpuTestProfile> Profiles =>
        [PpuTestProfile.Dmg, PpuTestProfile.CgbDmgCompatibility, PpuTestProfile.Cgb];

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_ImmediatelyAfterBoot_RoundTripsEveryProfile(PpuTestProfile profile)
    {
        var hardwareProfile = CreateProfile(profile);
        var source = CreatePpu(hardwareProfile, out _);
        var destination = CreatePpu(hardwareProfile, out _);
        var state = source.CaptureState();

        destination.RestoreState(state);

        var restoredState = destination.CaptureState();

        restoredState.Engine.GetType().Should().Be(state.Engine.GetType());
        restoredState.Should().BeEquivalentTo(state, options => options.WithStrictOrdering());
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
        var cgb = captured.Engine.Should().BeOfType<CgbPpuEngineState>().Subject;
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

        var recaptured = ppu.CaptureState().Engine.Should().BeOfType<CgbPpuEngineState>().Subject;
        ppu.CaptureState().VideoRam.Banks.Should().Equal(expectedVideoRam);
        ppu.CaptureState().BackgroundPaletteRam.Bytes.Should().Equal(expectedBackgroundPalette);
        ppu.CaptureState().ObjectPaletteRam.Bytes.Should().Equal(expectedObjectPalette);
        ppu.CaptureState().ObjectAttributeMemory.Bytes.Should().Equal(expectedOam);
        recaptured.Common.FrameBuffer.Should().Equal(expectedFrameBuffer);
        recaptured.BackgroundColorFifo.Should().Equal(expectedBackgroundColors);
        recaptured.BackgroundAttributeFifo.Should().Equal(expectedBackgroundAttributes);
        recaptured.Objects.Selector.Objects.Should().Equal(expectedObjects);
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

        FluentActions
            .Invoking(() => destination.RestoreState(malformed))
            .Should()
            .ThrowExactly<ArgumentException>();
        var afterMalformed = destination.CaptureState();
        afterMalformed.Engine.GetType().Should().Be(before.Engine.GetType());
        afterMalformed.Should().BeEquivalentTo(before, options => options.WithStrictOrdering());

        var wrongEngine = source.CaptureState() with
        {
            Engine = new DmgPixelRulesPpuEngine<DmgShadePixelOutput>(
                requestsMode2InterruptBeforeVBlank: false,
                stateWrapper: static s => new DmgPpuEngineState(s)
            ).CaptureState(),
        };

        FluentActions
            .Invoking(() => destination.RestoreState(wrongEngine))
            .Should()
            .ThrowExactly<ArgumentException>();
        var afterWrongEngine = destination.CaptureState();
        afterWrongEngine.Engine.GetType().Should().Be(before.Engine.GetType());
        afterWrongEngine.Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_MidBackgroundFifoAndPartialFramebuffer_ContinuesIdentically(
        PpuTestProfile profile
    )
    {
        var hardwareProfile = CreateProfile(profile);
        var source = CreatePpu(hardwareProfile, out var sourceInterrupts);
        ConfigureBackground(source);
        source.WriteRegister(AddressMap.LcdStatusRegister, 0x78);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);
        sourceInterrupts.SetInterruptFlag(0);

        var state = source.CaptureState();
        (GetCommon(state.Engine).RenderedPixels > 0).Should().BeTrue();
        (GetCommon(state.Engine).BackgroundWindowFetcher.BackgroundFifoCount > 0).Should().BeTrue();

        var restored = CreatePpu(hardwareProfile, out var restoredInterrupts);
        restored.RestoreState(state);

        restoredInterrupts.InterruptFlag.Should().Be(0);
        restored.Tick(0).CompletedFrame.Should().BeNull();
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
        frame.PixelFormat.Should().Be(LcdPixelFormat.Rgb555Le);
        frame.Pixels.Span[0].Should().Be(0x34);
        frame.Pixels.Span[1].Should().Be(0x12);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_MidWindowFetch_ContinuesIdentically(PpuTestProfile profile)
    {
        var hardwareProfile = CreateProfile(profile);
        var source = CreatePpu(hardwareProfile, out var sourceInterrupts);
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
        GetCommon(state.Engine).BackgroundWindowFetcher.WindowActiveThisLine.Should().BeTrue();
        var restored = CreatePpu(hardwareProfile, out var restoredInterrupts);
        restored.RestoreState(state);

        DriveIdenticallyToCompletedFrame(source, sourceInterrupts, restored, restoredInterrupts);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void RestoreState_AfterObjectSelection_ContinuesIdentically(PpuTestProfile profile)
    {
        var hardwareProfile = CreateProfile(profile);
        var source = CreatePpu(hardwareProfile, out var sourceInterrupts);
        ConfigureBackground(source);
        WriteObject(source, 0, y: 16, x: 16, tile: 0, flags: 0);
        source.WriteRegister(
            AddressMap.LcdControlRegister,
            LcdEnable | BackgroundEnable | ObjectEnable | UnsignedBackgroundTileData
        );
        source.Tick(100);
        sourceInterrupts.SetInterruptFlag(0);

        var state = source.CaptureState();
        GetObjects(state.Engine).Selected.Should().BeTrue();
        var restored = CreatePpu(hardwareProfile, out var restoredInterrupts);
        restored.RestoreState(state);

        DriveIdenticallyToCompletedFrame(source, sourceInterrupts, restored, restoredInterrupts);
    }

    [Fact]
    public void ValidateState_RejectsInvalidStatusAndObjectPriorityMode()
    {
        var cgb = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb), out _);
        var cgbState = cgb.CaptureState();

        FluentActions
            .Invoking(() => cgb.ValidateState(cgbState with { StatusInterruptSelect = 0x80 }))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() =>
                cgb.ValidateState(cgbState with { ObjectPriorityMode = (ObjectPriorityMode)2 })
            )
            .Should()
            .ThrowExactly<ArgumentException>();

        var dmg = CreatePpu(DmgHardwareProfile.Instance, out _);
        FluentActions
            .Invoking(() =>
                dmg.ValidateState(
                    dmg.CaptureState() with
                    {
                        ObjectPriorityMode = ObjectPriorityMode.LowerXWins,
                    }
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void RestoreState_MidDmgStatWriteQuirkPreservesRemainingCycle()
    {
        var profile = DmgHardwareProfile.Instance;
        var source = CreatePpu(profile, out var sourceInterrupts);
        source.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        source.WriteRegister(AddressMap.LcdStatusRegister, 0x20);
        source.Tick(2);
        sourceInterrupts.SetInterruptFlag(0);

        var state = source.CaptureState();
        var restored = CreatePpu(profile, out var restoredInterrupts);
        restored.RestoreState(state);

        state.StatWriteQuirkTCyclesRemaining.Should().Be(2);
        state.StatusInterruptSelect.Should().Be(0x20);
        restored.Tick(1);
        restored.CaptureState().StatWriteQuirkTCyclesRemaining.Should().Be(1);
        (
            restored.ReadRegister(AddressMap.LcdStatusRegister)
            & PpuStatusRegister.InterruptSelectMask
        )
            .Should()
            .Be(PpuStatusRegister.InterruptSelectMask);

        restored.Tick(1);
        restored.CaptureState().StatWriteQuirkTCyclesRemaining.Should().Be(0);
        (
            restored.ReadRegister(AddressMap.LcdStatusRegister)
            & PpuStatusRegister.InterruptSelectMask
        )
            .Should()
            .Be(0x20);
        restoredInterrupts.InterruptFlag.Should().Be(0x00);
    }

    [Fact]
    public void RestoreState_RejectsDmgStatWriteQuirkOnCgbWithoutMutation()
    {
        var cgb = CreatePpu(new CgbHardwareProfile(CgbOperatingMode.Cgb), out _);
        var before = cgb.CaptureState();
        var invalid = before with
        {
            Control = LcdEnable,
            StatusInterruptSelect = PpuStatusRegister.InterruptSelectMask,
            StatWriteQuirkTCyclesRemaining = 1,
        };

        FluentActions
            .Invoking(() => cgb.RestoreState(invalid))
            .Should()
            .ThrowExactly<ArgumentException>();
        cgb.CaptureState().Should().BeEquivalentTo(before);
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

        restored.VideoRenderingEnabled.Should().BeFalse();
        var frame = DriveIdenticallyToCompletedFrame(
            source,
            sourceInterrupts,
            restored,
            restoredInterrupts
        );
        frame.Pixels.Span.ToArray().Should().NotBeEmpty();
    }

    private static IHardwareProfile CreateProfile(PpuTestProfile profile) =>
        profile switch
        {
            PpuTestProfile.Dmg => DmgHardwareProfile.Instance,
            PpuTestProfile.CgbDmgCompatibility => new CgbHardwareProfile(
                CgbOperatingMode.DmgCompatibility
            ),
            PpuTestProfile.Cgb => new CgbHardwareProfile(CgbOperatingMode.Cgb),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
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
            profile.IsObjectPriorityModeRegisterEnabled,
            profile.HasDmgStatWriteInterruptQuirk
        );
    }

    private static void ConfigureBackground(PpuController ppu)
    {
        WriteTileRow(ppu, AddressMap.VideoRamStart, 0, lowByte: 0xAA, highByte: 0x55);
        ppu.VideoRam.Write(0x9800, 0);
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

            restoredResult.Interrupts.Should().Be(sourceResult.Interrupts);
            restoredResult.EnteredVisibleHBlank.Should().Be(sourceResult.EnteredVisibleHBlank);
            restoredInterrupts.InterruptFlag.Should().Be(sourceInterrupts.InterruptFlag);
            restored
                .ReadRegister(AddressMap.LcdYCoordinateRegister)
                .Should()
                .Be(source.ReadRegister(AddressMap.LcdYCoordinateRegister));
            restored
                .ReadRegister(AddressMap.LcdStatusRegister)
                .Should()
                .Be(source.ReadRegister(AddressMap.LcdStatusRegister));
            restored.IsCpuVideoRamReadBlocked.Should().Be(source.IsCpuVideoRamReadBlocked);
            restored
                .IsCpuObjectAttributeMemoryReadBlocked.Should()
                .Be(source.IsCpuObjectAttributeMemoryReadBlocked);
            (restoredResult.CompletedFrame is null)
                .Should()
                .Be(sourceResult.CompletedFrame is null);

            if (
                sourceResult.CompletedFrame is { } sourceFrame
                && restoredResult.CompletedFrame is { } restoredFrame
            )
            {
                restoredFrame.PixelFormat.Should().Be(sourceFrame.PixelFormat);
                restoredFrame.Pixels.ToArray().Should().Equal(sourceFrame.Pixels.ToArray());
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
}
