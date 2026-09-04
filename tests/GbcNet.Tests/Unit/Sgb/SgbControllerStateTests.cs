// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Snes;
using static GbcNet.Tests.Unit.Sgb.SgbTestHelpers;

namespace GbcNet.Tests.Unit.Sgb;

public sealed class SgbControllerStateTests
{
    [Fact]
    public void CaptureRestore_RetainsEveryLogicalValueAndDefensivelyOwnsBuffers()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var state = CreateState();
        var expected = CloneState(state);

        sgb.RestoreState(state);
        MutateBuffers(state);
        sgb.CaptureState()
            .Should()
            .BeEquivalentTo(expected, options => options.WithStrictOrdering());

        var captured = sgb.CaptureState();
        MutateBuffers(captured);
        sgb.CaptureState()
            .Should()
            .BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    [Fact]
    public void RestoreState_IsAtomicWhenLateAttributeValidationFails()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        var before = sgb.CaptureState();
        var invalidAttributes = (byte[])before.Renderer.AttributeMap.Clone();
        invalidAttributes[^1] = 4;

        FluentActions
            .Invoking(() =>
                sgb.RestoreState(
                    before with
                    {
                        Command = new byte[112],
                        Renderer = before.Renderer with { AttributeMap = invalidAttributes },
                    }
                )
            )
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();

        sgb.CaptureState().Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
        Rgb555Assertions.PixelEquals(
            sgb.ApplyPalettes(CreateDmgFrame(shade: 2)),
            0,
            expected: 0x3333
        );
    }

    [Fact]
    public void RestoreState_RejectsImpossiblePacketPhase()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var before = sgb.CaptureState();

        FluentActions
            .Invoking(() =>
                sgb.RestoreState(
                    before with
                    {
                        CommandWriteBitIndex = 0,
                        PacketPhase = SgbPacketPhase.AwaitingStop,
                    }
                )
            )
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();

        sgb.CaptureState().Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Fact]
    public void RestoreState_ContinuesPartialCommand()
    {
        var packet = CreatePacket(command: 0x00, Pal01Payload);
        var original = new SgbController(commandsEnabled: true);
        var selectedGroups = (byte)0x30;
        WriteSgbStartPulse(original, ref selectedGroups);
        WriteBits(original, ref selectedGroups, packet, count: 10);
        var resumed = new SgbController(commandsEnabled: true);
        resumed.RestoreState(original.CaptureState());
        var resumedGroups = selectedGroups;

        WriteBits(original, ref selectedGroups, packet, start: 10);
        WriteSgbBit(original, ref selectedGroups, value: false);
        WriteBits(resumed, ref resumedGroups, packet, start: 10);
        WriteSgbBit(resumed, ref resumedGroups, value: false);

        Rgb555Assertions.PixelEquals(
            original.ApplyPalettes(CreateDmgFrame(shade: 2)),
            0,
            expected: 0x3333
        );
        resumed
            .ApplyPalettes(CreateDmgFrame(shade: 2))
            .Pixels.ToArray()
            .Should()
            .Equal(original.ApplyPalettes(CreateDmgFrame(shade: 2)).Pixels.ToArray());
    }

    [Fact]
    public void RestoreState_ContinuesUnknownCommandBetweenPackets()
    {
        // Pan Docs `command-packet-transfers.md`: continuation packets have no command header.
        var original = new SgbController(commandsEnabled: true);
        WriteSgbPacket(original, CreatePacket(command: 0x1E, [], packetCount: 2));
        var resumed = new SgbController(commandsEnabled: true);
        resumed.RestoreState(original.CaptureState());
        var headerLikeContinuation = CreatePacket(command: 0x17, [0x02]);

        WriteSgbPacket(original, headerLikeContinuation);
        WriteSgbPacket(resumed, headerLikeContinuation);
        WriteSgbPacket(original, command: 0x00, Pal01Payload);
        WriteSgbPacket(resumed, command: 0x00, Pal01Payload);

        var originalFrame = original.ApplyPalettes(CreateDmgFrame(shade: 2));
        var resumedFrame = resumed.ApplyPalettes(CreateDmgFrame(shade: 2));
        Rgb555Assertions.PixelEquals(originalFrame, 0, expected: 0x3333);
        resumedFrame.Pixels.ToArray().Should().Equal(originalFrame.Pixels.ToArray());
    }

    [Fact]
    public void RestoreState_ContinuesPendingTransferCountdown()
    {
        var original = new SgbController(commandsEnabled: true);
        WriteSgbPacket(original, command: 0x0B, []);
        var resumed = new SgbController(commandsEnabled: true);
        resumed.RestoreState(original.CaptureState());

        for (var frame = 0; frame < 2; frame++)
        {
            original.ApplyPendingVramTransfer(CreateDmgFrame(shade: 0));
            resumed.ApplyPendingVramTransfer(CreateDmgFrame(shade: 0));
            original.HasPendingVramTransfer.Should().BeTrue();
            resumed.HasPendingVramTransfer.Should().BeTrue();
        }

        original.ApplyPendingVramTransfer(CreateDmgFrame(shade: 0));
        resumed.ApplyPendingVramTransfer(CreateDmgFrame(shade: 0));
        original.HasPendingVramTransfer.Should().BeFalse();
        resumed.HasPendingVramTransfer.Should().BeFalse();
    }

    [Fact]
    public void RestoreState_ContinuesPaletteAndAttributeTransfers()
    {
        var paletteTransfer = new byte[4096];
        var attributeTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 5, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteSystemPalette(paletteTransfer, paletteId: 6, 0x5555, 0x6666, 0x7777, 0x7FFF);
        WriteAttributeFile(attributeTransfer, fileIndex: 3, packedFirstFourTiles: 0x40);
        var sgb = new SgbController(commandsEnabled: true);

        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb = RestoreIntoNewController(sgb);

        for (var transferFrame = 0; transferFrame < 3; transferFrame++)
        {
            sgb.ApplyPendingVramTransfer(paletteTransfer);
        }

        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 6, 5, 5));
        WriteSgbPacket(sgb, command: 0x15, []);
        sgb = RestoreIntoNewController(sgb);

        for (var transferFrame = 0; transferFrame < 3; transferFrame++)
        {
            sgb.ApplyPendingVramTransfer(attributeTransfer);
        }

        WriteSgbPacket(sgb, command: 0x16, [0x03]);

        var frame = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));
        Rgb555Assertions.PixelEquals(frame, 0, expected: 0x7777);
        Rgb555Assertions.PixelEquals(frame, 8, expected: 0x3333);
    }

    [Fact]
    public void RestoreState_RetainsMaskAndFrameHistoriesWithoutEmittingAFrame()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, WhiteColorZeroPal01Payload);
        sgb.ApplyPalettes(CreateDmgFrame(shade: 1));
        WriteSgbPacket(sgb, command: 0x17, [0x01]);
        var state = sgb.CaptureState();
        var resumed = new SgbController(commandsEnabled: true);

        resumed.RestoreState(state);
        resumed
            .CaptureState()
            .Should()
            .BeEquivalentTo(state, options => options.WithStrictOrdering());
        Rgb555Assertions.PixelEquals(
            resumed.ApplyPalettes(CreateDmgFrame(shade: 2)),
            0,
            expected: 0x2222
        );
        WriteSgbPacket(resumed, command: 0x17, [0x00]);
        Rgb555Assertions.PixelEquals(
            resumed.ApplyPalettes(CreateDmgFrame(shade: 0)),
            0,
            expected: 0x2222
        );
    }

    [Fact]
    public void RestoreState_RegeneratesBorderCacheAndOverlay()
    {
        var tiles = new byte[4096];
        var map = new byte[4096];
        WriteBorderTilePixel(tiles, tileIndex: 1, color: 5);
        WriteBorderMapEntry(map, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(map, paletteColor: 5, 0x1234);
        WriteBorderMapEntry(map, tileX: 7, tileY: 5, tileIndex: 1, palette: 4);
        var source = new SgbController(commandsEnabled: true);
        ApplyBorderTransfers(source, tiles, map);
        source.ApplyPalettes(CreateDmgFrame(shade: 0));
        var targetTiles = new byte[4096];
        var targetMap = new byte[4096];
        WriteBorderTilePixel(targetTiles, tileIndex: 1, color: 5);
        WriteBorderMapEntry(targetMap, tileX: 6, tileY: 5, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(targetMap, paletteColor: 5, 0x5678);
        var target = new SgbController(commandsEnabled: true);
        ApplyBorderTransfers(target, targetTiles, targetMap);
        target.ApplyPalettes(CreateDmgFrame(shade: 0));

        target.RestoreState(source.CaptureState());
        var restored = target.ApplyPalettes(CreateDmgFrame(shade: 0));

        Rgb555Assertions.PixelEquals(restored, 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(restored, SgbGameBoyPixelIndex(x: 0, y: 0), expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(restored, SgbGameBoyPixelIndex(x: 8, y: 0), expected: 0x1234);
    }

    [Fact]
    public void RestoreState_ContinuesMultiplayerRotation()
    {
        var original = new SgbController(commandsEnabled: true);
        WriteSgbPacket(original, command: 0x11, [0x03]);
        original.Write(0x20, previousSelectedGroups: 0x00);
        var resumed = RestoreIntoNewController(original);

        original.Write(0x20, previousSelectedGroups: 0x00);
        resumed.Write(0x20, previousSelectedGroups: 0x00);

        resumed.ReadLowNibble(0x30, 0x0F).Should().Be(original.ReadLowNibble(0x30, 0x0F));
        resumed.ReadLowNibble(0x30, 0x0F).Should().Be(0x0D);
    }

    private static SgbController RestoreIntoNewController(SgbController source)
    {
        var target = new SgbController(commandsEnabled: true);
        target.RestoreState(source.CaptureState());
        return target;
    }

    private static SgbControllerState CreateState()
    {
        var state = new SgbController(commandsEnabled: true).CaptureState();
        state.Command[0] = 0x5A;
        state.Renderer.SystemPalettes[0] = 0x1234;
        state.Renderer.AttributeFiles[0] = 0x5A;
        state.Renderer.BorderTiles[0] = 0x5A;
        state.Renderer.BorderMap[0] = 0x1234;
        state.Renderer.BorderPalettes[0] = 0x1234;
        state.Renderer.Palettes[0] = 0x1234;
        state.Renderer.AttributeMap[0] = 3;
        return state with
        {
            CommandWriteBitIndex = 128,
            PacketPhase = SgbPacketPhase.AwaitingStop,
            PlayerCount = 4,
            CurrentPlayer = 3,
            MaskMode = 3,
            PendingVramTransfer = 5,
            PendingVramTransferFrameDelay = 2,
            Renderer = state.Renderer with
            {
                BorderReady = true,
                VisibleFramePixels = CreateHistory(0x5A),
                LastBootFramePixels = CreateHistory(0xA5),
            },
        };
    }

    private static SgbControllerState CloneState(SgbControllerState state) =>
        state with
        {
            Command = (byte[])state.Command.Clone(),
            Renderer = state.Renderer with
            {
                SystemPalettes = (ushort[])state.Renderer.SystemPalettes.Clone(),
                AttributeFiles = (byte[])state.Renderer.AttributeFiles.Clone(),
                BorderTiles = (byte[])state.Renderer.BorderTiles.Clone(),
                BorderMap = (ushort[])state.Renderer.BorderMap.Clone(),
                BorderPalettes = (ushort[])state.Renderer.BorderPalettes.Clone(),
                Palettes = (ushort[])state.Renderer.Palettes.Clone(),
                AttributeMap = (byte[])state.Renderer.AttributeMap.Clone(),
                VisibleFramePixels = state.Renderer.VisibleFramePixels is null
                    ? null
                    : (byte[])state.Renderer.VisibleFramePixels.Clone(),
                LastBootFramePixels = state.Renderer.LastBootFramePixels is null
                    ? null
                    : (byte[])state.Renderer.LastBootFramePixels.Clone(),
            },
        };

    private static void MutateBuffers(SgbControllerState state)
    {
        state.Command[0]++;
        state.Renderer.SystemPalettes[0]++;
        state.Renderer.AttributeFiles[0]++;
        state.Renderer.BorderTiles[0]++;
        state.Renderer.BorderMap[0]++;
        state.Renderer.BorderPalettes[0]++;
        state.Renderer.Palettes[0]++;
        state.Renderer.AttributeMap[0] = 0;
        state.Renderer.VisibleFramePixels![0]++;
        state.Renderer.LastBootFramePixels![0]++;
    }

    private static byte[] CreateHistory(byte value)
    {
        var history = new byte[160 * 144 * 2];
        Array.Fill(history, value);
        return history;
    }
}
