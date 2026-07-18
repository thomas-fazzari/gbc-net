// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Ppu;
using GbcNet.Core.Snes;

namespace GbcNet.Tests.Sgb;

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
        Assert.Equivalent(expected, sgb.CaptureState(), strict: true);

        var captured = sgb.CaptureState();
        MutateBuffers(captured);
        Assert.Equivalent(expected, sgb.CaptureState(), strict: true);
    }

    [Fact]
    public void RestoreState_IsAtomicWhenLateAttributeValidationFails()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        var before = sgb.CaptureState();
        var invalidAttributes = (byte[])before.AttributeMap.Clone();
        invalidAttributes[^1] = 4;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            sgb.RestoreState(
                before with
                {
                    Command = new byte[112],
                    AttributeMap = invalidAttributes,
                }
            )
        );

        Assert.Equivalent(before, sgb.CaptureState(), strict: true);
        Rgb555Assertions.PixelEquals(
            sgb.ApplyPalettes(CreateDmgFrame(shade: 2)),
            0,
            expected: 0x3333
        );
    }

    [Fact]
    public void RestoreState_ContinuesPartialCommand()
    {
        Span<byte> packet = stackalloc byte[16];
        packet[0] = 0x01;
        Pal01Payload.CopyTo(packet[1..]);
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
        Assert.Equal(
            original.ApplyPalettes(CreateDmgFrame(shade: 2)).Pixels.ToArray(),
            resumed.ApplyPalettes(CreateDmgFrame(shade: 2)).Pixels.ToArray()
        );
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
            Assert.True(original.HasPendingVramTransfer);
            Assert.True(resumed.HasPendingVramTransfer);
        }

        original.ApplyPendingVramTransfer(CreateDmgFrame(shade: 0));
        resumed.ApplyPendingVramTransfer(CreateDmgFrame(shade: 0));
        Assert.False(original.HasPendingVramTransfer);
        Assert.False(resumed.HasPendingVramTransfer);
    }

    [Fact]
    public void RestoreState_ContinuesPaletteAndAttributeTransfers()
    {
        var paletteTransfer = new byte[4096];
        var attributeTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 5, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteSystemPalette(paletteTransfer, paletteId: 6, 0x5555, 0x6666, 0x7777, 0x7FFF);
        attributeTransfer[3 * 90] = 0x40;
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
        Assert.Equivalent(state, resumed.CaptureState(), strict: true);
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
        WriteUInt16(map, 0, (4 << 10) | 1);
        WriteUInt16(map, 0x800 + (5 * 2), 0x1234);
        WriteUInt16(map, (7 + (5 * 32)) * 2, (4 << 10) | 1);
        var source = new SgbController(commandsEnabled: true);
        ApplyBorderTransfers(source, tiles, map);
        source.ApplyPalettes(CreateDmgFrame(shade: 0));
        var targetTiles = new byte[4096];
        var targetMap = new byte[4096];
        WriteBorderTilePixel(targetTiles, tileIndex: 1, color: 5);
        WriteUInt16(targetMap, (6 + (5 * 32)) * 2, (4 << 10) | 1);
        WriteUInt16(targetMap, 0x800 + (5 * 2), 0x5678);
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

        Assert.Equal(original.ReadLowNibble(0x30, 0x0F), resumed.ReadLowNibble(0x30, 0x0F));
        Assert.Equal((byte)0x0D, resumed.ReadLowNibble(0x30, 0x0F));
    }

    private static ReadOnlySpan<byte> Pal01Payload =>
        [0x11, 0x11, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

    private static ReadOnlySpan<byte> WhiteColorZeroPal01Payload =>
        [0xFF, 0x7F, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

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
        state.SystemPalettes[0] = 0x1234;
        state.AttributeFiles[0] = 0x5A;
        state.BorderTiles[0] = 0x5A;
        state.BorderMap[0] = 0x1234;
        state.BorderPalettes[0] = 0x1234;
        state.Palettes[0] = 0x1234;
        state.AttributeMap[0] = 3;
        return state with
        {
            CommandWriteBitIndex = 896,
            ReadyForPulse = true,
            ReadyForWrite = true,
            ReadyForStop = true,
            PlayerCount = 4,
            CurrentPlayer = 3,
            MaskMode = 3,
            PendingVramTransfer = 5,
            PendingVramTransferFrameDelay = 2,
            BorderReady = true,
            VisibleFramePixels = CreateHistory(0x5A),
            LastBootFramePixels = CreateHistory(0xA5),
        };
    }

    private static SgbControllerState CloneState(SgbControllerState state) =>
        state with
        {
            Command = (byte[])state.Command.Clone(),
            SystemPalettes = (ushort[])state.SystemPalettes.Clone(),
            AttributeFiles = (byte[])state.AttributeFiles.Clone(),
            BorderTiles = (byte[])state.BorderTiles.Clone(),
            BorderMap = (ushort[])state.BorderMap.Clone(),
            BorderPalettes = (ushort[])state.BorderPalettes.Clone(),
            Palettes = (ushort[])state.Palettes.Clone(),
            AttributeMap = (byte[])state.AttributeMap.Clone(),
            VisibleFramePixels = state.VisibleFramePixels is null
                ? null
                : (byte[])state.VisibleFramePixels.Clone(),
            LastBootFramePixels = state.LastBootFramePixels is null
                ? null
                : (byte[])state.LastBootFramePixels.Clone(),
        };

    private static void MutateBuffers(SgbControllerState state)
    {
        state.Command[0]++;
        state.SystemPalettes[0]++;
        state.AttributeFiles[0]++;
        state.BorderTiles[0]++;
        state.BorderMap[0]++;
        state.BorderPalettes[0]++;
        state.Palettes[0]++;
        state.AttributeMap[0] = 0;
        state.VisibleFramePixels![0]++;
        state.LastBootFramePixels![0]++;
    }

    private static byte[] CreateHistory(byte value)
    {
        var history = new byte[160 * 144 * 2];
        Array.Fill(history, value);
        return history;
    }

    private static void ApplyBorderTransfers(SgbController sgb, byte[] tiles, byte[] map)
    {
        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.ApplyPendingVramTransfer(tiles);
        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.ApplyPendingVramTransfer(map);
    }

    private static LcdFrame CreateDmgFrame(byte shade)
    {
        var pixels = new byte[160 * 144];
        Array.Fill(pixels, shade);
        return new LcdFrame(160, 144, LcdPixelFormat.DmgShadeIndex8, pixels);
    }

    private static byte[] CreatePalSetPayload(
        ushort palette0,
        ushort palette1,
        ushort palette2,
        ushort palette3
    )
    {
        var payload = new byte[15];
        WriteUInt16(payload, 0, palette0);
        WriteUInt16(payload, 2, palette1);
        WriteUInt16(payload, 4, palette2);
        WriteUInt16(payload, 6, palette3);
        return payload;
    }

    private static void WriteSystemPalette(
        byte[] bytes,
        int paletteId,
        ushort color0,
        ushort color1,
        ushort color2,
        ushort color3
    )
    {
        var offset = paletteId * 8;
        WriteUInt16(bytes, offset, color0);
        WriteUInt16(bytes, offset + 2, color1);
        WriteUInt16(bytes, offset + 4, color2);
        WriteUInt16(bytes, offset + 6, color3);
    }

    private static void WriteBorderTilePixel(byte[] bytes, int tileIndex, byte color)
    {
        var offset = tileIndex * 32;
        bytes[offset] = (byte)(((color & 0x01) != 0 ? 0x80 : 0) | 0);
        bytes[offset + 1] = (byte)(((color & 0x02) != 0 ? 0x80 : 0) | 0);
        bytes[offset + 16] = (byte)(((color & 0x04) != 0 ? 0x80 : 0) | 0);
        bytes[offset + 17] = (byte)(((color & 0x08) != 0 ? 0x80 : 0) | 0);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static int SgbGameBoyPixelIndex(int x, int y) => ((40 + y) * 256) + 48 + x;

    private static void WriteSgbPacket(SgbController sgb, byte command, ReadOnlySpan<byte> payload)
    {
        Span<byte> packet = stackalloc byte[16];
        packet[0] = (byte)((command << 3) | 0x01);
        payload.CopyTo(packet[1..]);
        var selectedGroups = (byte)0x30;
        WriteSgbStartPulse(sgb, ref selectedGroups);
        WriteBits(sgb, ref selectedGroups, packet);
        WriteSgbBit(sgb, ref selectedGroups, value: false);
    }

    private static void WriteBits(
        SgbController sgb,
        ref byte selectedGroups,
        ReadOnlySpan<byte> bytes,
        int start = 0,
        int count = int.MaxValue
    )
    {
        var end = (int)Math.Min(bytes.Length * 8L, (long)start + count);
        for (var bit = start; bit < end; bit++)
        {
            WriteSgbBit(sgb, ref selectedGroups, (bytes[bit / 8] & (1 << (bit & 7))) != 0);
        }
    }

    private static void WriteSgbStartPulse(SgbController sgb, ref byte selectedGroups)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x00);
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
    }

    private static void WriteSgbBit(SgbController sgb, ref byte selectedGroups, bool value)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
        WriteSgbJoyp(sgb, ref selectedGroups, value ? (byte)0x10 : (byte)0x20);
    }

    private static void WriteSgbJoyp(SgbController sgb, ref byte selectedGroups, byte value)
    {
        sgb.Write(value, selectedGroups);
        selectedGroups = (byte)(value & 0x30);
    }
}
