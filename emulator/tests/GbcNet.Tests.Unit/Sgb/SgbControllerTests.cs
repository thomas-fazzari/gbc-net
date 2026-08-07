// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Snes;
using static GbcNet.Tests.Unit.Sgb.SgbTestHelpers;

namespace GbcNet.Tests.Unit.Sgb;

public sealed class SgbControllerTests
{
    [Fact]
    public void ApplyPalettes_UsesPal01ColorsAndLineAttributes()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x05, [0x01, 0xA0]);

        var frame = CreateDmgFrame(shade: 2);
        var colorized = sgb.ApplyPalettes(frame);

        colorized.Width.Should().Be(160);
        colorized.Height.Should().Be(144);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x6666);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 8), expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_UsesDivisionAttributes()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x06, [0x10, 0x01]);

        var frame = CreateDmgFrame(shade: 3);
        var colorized = sgb.ApplyPalettes(frame);

        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x4444);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 8, y: 0), expected: 0x7777);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 16, y: 0), expected: 0x4444);
    }

    [Fact]
    public void ApplyPalettes_MaskFreezeKeepsPreviousVisibleFrame()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);

        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));
        WriteSgbPacket(sgb, command: 0x17, [0x01]);
        var frozenFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));
        WriteSgbPacket(sgb, command: 0x17, [0x00]);
        var currentFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        Rgb555Assertions.PixelEquals(firstFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x2222);
        Rgb555Assertions.PixelEquals(frozenFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x2222);
        Rgb555Assertions.PixelEquals(currentFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_MaskBlankOutputsBlackOrColorZero()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);

        WriteSgbPacket(sgb, command: 0x17, [0x02]);
        var blackFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));
        WriteSgbPacket(sgb, command: 0x17, [0x03]);
        var colorZeroFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        Rgb555Assertions.PixelEquals(blackFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x0000);
        Rgb555Assertions.PixelEquals(
            colorZeroFrame,
            GameBoyPixelIndex(x: 0, y: 0),
            expected: 0x1111
        );
    }

    [Fact]
    public void ApplyPendingVramTransfer_LoadsSystemPalettesUsedByPalSet()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var transferData = new byte[4096];
        WriteSystemPalette(transferData, paletteId: 5, 0x1111, 0x2222, 0x3333, 0x4444);

        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.HasPendingVramTransfer.Should().BeTrue();
        sgb.ApplyPendingVramTransfer(transferData);
        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 5, 5, 5));

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        sgb.HasPendingVramTransfer.Should().BeFalse();
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_PalSetCanCancelMask()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var transferData = new byte[4096];
        WriteSystemPalette(transferData, paletteId: 7, 0x1111, 0x2222, 0x3333, 0x4444);

        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.ApplyPendingVramTransfer(transferData);
        WriteSgbPacket(sgb, command: 0x17, [0x02]);
        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(7, 7, 7, 7, flags: 0x40));

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x2222);
    }

    [Fact]
    public void Write_DataSndDoesNotRequestVramTransfer()
    {
        var sgb = new SgbController(commandsEnabled: true);

        WriteSgbPacket(sgb, command: 0x0F, [0x00, 0x18, 0x00, 0x01, 0x42]);

        sgb.HasPendingVramTransfer.Should().BeFalse();
    }

    [Fact]
    public void ApplyPendingVramTransfer_LoadsAttributeFilesUsedByAttrSet()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var transferData = new byte[4096];
        WriteAttributeFile(transferData, fileIndex: 3, packedFirstFourTiles: 0x40);

        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x15, []);
        sgb.HasPendingVramTransfer.Should().BeTrue();
        sgb.ApplyPendingVramTransfer(transferData);
        WriteSgbPacket(sgb, command: 0x16, [0x03]);

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        sgb.HasPendingVramTransfer.Should().BeFalse();
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x6666);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_PalSetCanApplyAttributeFile()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var paletteTransfer = new byte[4096];
        var attributeTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 5, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteSystemPalette(paletteTransfer, paletteId: 6, 0x5555, 0x6666, 0x7777, 0x7FFF);
        WriteAttributeFile(attributeTransfer, fileIndex: 4, packedFirstFourTiles: 0x40);

        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.ApplyPendingVramTransfer(paletteTransfer);
        WriteSgbPacket(sgb, command: 0x15, []);
        sgb.ApplyPendingVramTransfer(attributeTransfer);
        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 6, 5, 5, flags: 0x84));

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x7777);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void ApplyPendingVramTransfer_LoadsBorderTilesAndMap()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var tileTransfer = new byte[4096];
        var mapTransfer = new byte[4096];
        WriteBorderTilePixel(tileTransfer, tileIndex: 1, color: 5);
        WriteBorderMapEntry(mapTransfer, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(mapTransfer, paletteColor: 5, 0x1234);
        WriteBorderMapEntry(mapTransfer, tileX: 7, tileY: 5, tileIndex: 1, palette: 4);

        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.HasPendingVramTransfer.Should().BeTrue();
        sgb.ApplyPendingVramTransfer(tileTransfer);
        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.HasPendingVramTransfer.Should().BeTrue();
        sgb.ApplyPendingVramTransfer(mapTransfer);

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        sgb.HasPendingVramTransfer.Should().BeFalse();
        Rgb555Assertions.PixelEquals(colorized, pixelIndex: 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(colorized, SgbGameBoyPixelIndex(x: 0, y: 0), expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(colorized, SgbGameBoyPixelIndex(x: 8, y: 0), expected: 0x1234);
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterTileTransfer()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var firstTiles = new byte[4096];
        var updatedTiles = new byte[4096];
        var map = new byte[4096];
        WriteBorderTilePixel(firstTiles, tileIndex: 1, color: 5);
        WriteBorderTilePixel(updatedTiles, tileIndex: 1, color: 6);
        WriteBorderMapEntry(map, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(map, paletteColor: 5, 0x1234);
        WriteBorderPaletteColor(map, paletteColor: 6, 0x5678);
        ApplyBorderTransfers(sgb, firstTiles, map);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.ApplyPendingVramTransfer(updatedTiles);
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x5678);
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterMapTransfer()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var tiles = new byte[4096];
        var firstMap = new byte[4096];
        var updatedMap = new byte[4096];
        WriteBorderTilePixel(tiles, tileIndex: 1, color: 5);
        WriteBorderMapEntry(firstMap, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(firstMap, paletteColor: 5, 0x1234);
        WriteBorderMapEntry(updatedMap, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(updatedMap, paletteColor: 5, 0x5678);
        ApplyBorderTransfers(sgb, tiles, firstMap);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.ApplyPendingVramTransfer(updatedMap);
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x5678);
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterSharedColorZeroChanges()
    {
        var sgb = new SgbController(commandsEnabled: true);
        ApplyBorderTransfers(sgb, new byte[4096], new byte[4096]);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(
            firstFrame,
            SgbGameBoyPixelIndex(x: 0, y: 0),
            expected: 0x56B5
        );
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x1111);
        Rgb555Assertions.PixelEquals(
            updatedFrame,
            SgbGameBoyPixelIndex(x: 0, y: 0),
            expected: 0x2222
        );
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterPalSet()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var paletteTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 5, 0x1357, 0x2222, 0x3333, 0x4444);
        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.ApplyPendingVramTransfer(paletteTransfer);
        ApplyBorderTransfers(sgb, new byte[4096], new byte[4096]);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 5, 5, 5));
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x1357);
    }
}
