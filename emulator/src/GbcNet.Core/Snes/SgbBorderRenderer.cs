// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers;
using System.Buffers.Binary;
using GbcNet.Core.Ppu;

namespace GbcNet.Core.Snes;

/// <summary>
/// Owns SGB palette, attribute, border, and composed-frame state.
/// </summary>
internal sealed class SgbBorderRenderer
{
    private const int SgbScreenWidth = 256;
    private const int SgbScreenHeight = 224;
    private const int SgbGameBoyX = 48;
    private const int SgbGameBoyY = 40;
    private const int SgbBorderMapWidth = 32;
    private const int SgbBorderMapHeight = 28;
    private const int SgbBorderMapEntries = SgbBorderMapWidth * SgbBorderMapHeight;
    private const int SgbBorderTileBytes = 32;
    private const int SgbBorderTileTransferBytes = 4096;
    private const int SgbBorderPaletteOffset = 0x800;
    private const int SgbBorderPaletteColors = 16 * 3;
    private const int AttributeMapWidth = 20;
    private const int AttributeMapHeight = 18;
    private const int AttributeFilePackedSize = 90;
    private const int AttributeFileCount = 45;
    private const int AttributeFileTransferSizeBytes = AttributeFilePackedSize * AttributeFileCount;
    private const int Rgb555BytesPerPixel = 2;
    private const int SgbScreenPixelCount = SgbScreenWidth * SgbScreenHeight;
    private const int BorderOverlayMaskSizeBytes =
        ((PpuGeometry.FrameWidth * PpuGeometry.FrameHeight) + 7) / 8;
    private const byte MaskFreeze = 1;
    private const byte MaskBlack = 2;
    private const byte MaskColor0 = 3;

    private readonly ushort[] _systemPalettes = new ushort[512 * 4];
    private readonly byte[] _attributeFiles = new byte[AttributeFileTransferSizeBytes];
    private readonly byte[] _borderTiles = new byte[256 * SgbBorderTileBytes];
    private readonly ushort[] _borderMap = new ushort[SgbBorderMapEntries];
    private readonly ushort[] _borderPalettes = new ushort[16 * 4];
    private readonly byte[] _borderOverlayMask = new byte[BorderOverlayMaskSizeBytes];
    private readonly ushort[] _palettes =
    [
        0x7FFF,
        0x56B5,
        0x294A,
        0x0000,
        0x7FFF,
        0x56B5,
        0x294A,
        0x0000,
        0x7FFF,
        0x56B5,
        0x294A,
        0x0000,
        0x7FFF,
        0x56B5,
        0x294A,
        0x0000,
    ];
    private readonly byte[] _attributeMap = new byte[AttributeMapWidth * AttributeMapHeight];
    private bool _borderReady;
    private byte[]? _borderCachePixels;
    private byte[]? _borderGameBoyPixels;
    private bool _borderCacheDirty = true;
    private byte[]? _visibleFramePixels;
    private byte[]? _lastBootFramePixels;

    internal SgbBorderRendererState CaptureState() =>
        new(
            (ushort[])_systemPalettes.Clone(),
            (byte[])_attributeFiles.Clone(),
            (byte[])_borderTiles.Clone(),
            (ushort[])_borderMap.Clone(),
            (ushort[])_borderPalettes.Clone(),
            (ushort[])_palettes.Clone(),
            (byte[])_attributeMap.Clone(),
            _borderReady,
            _visibleFramePixels is null ? null : (byte[])_visibleFramePixels.Clone(),
            _lastBootFramePixels is null ? null : (byte[])_lastBootFramePixels.Clone()
        );

    internal void ValidateState(SgbBorderRendererState state)
    {
        if (
            state.SystemPalettes is null
            || state.SystemPalettes.Length != _systemPalettes.Length
            || state.AttributeFiles is null
            || state.AttributeFiles.Length != _attributeFiles.Length
            || state.BorderTiles is null
            || state.BorderTiles.Length != _borderTiles.Length
            || state.BorderMap is null
            || state.BorderMap.Length != _borderMap.Length
            || state.BorderPalettes is null
            || state.BorderPalettes.Length != _borderPalettes.Length
            || state.Palettes is null
            || state.Palettes.Length != _palettes.Length
            || state.AttributeMap is null
            || state.AttributeMap.Length != _attributeMap.Length
            || (
                state.VisibleFramePixels is not null
                && state.VisibleFramePixels.Length
                    != PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
            )
            || (
                state.LastBootFramePixels is not null
                && state.LastBootFramePixels.Length
                    != PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
            )
        )
        {
            throw new ArgumentException(
                "SGB renderer state has an invalid buffer shape.",
                nameof(state)
            );
        }

        if (state.AttributeMap.Any(static attribute => attribute > 3))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal void RestoreState(SgbBorderRendererState state)
    {
        ValidateState(state);

        state.SystemPalettes.CopyTo(_systemPalettes, 0);
        state.AttributeFiles.CopyTo(_attributeFiles, 0);
        state.BorderTiles.CopyTo(_borderTiles, 0);
        state.BorderMap.CopyTo(_borderMap, 0);
        state.BorderPalettes.CopyTo(_borderPalettes, 0);
        state.Palettes.CopyTo(_palettes, 0);
        state.AttributeMap.CopyTo(_attributeMap, 0);
        _borderReady = state.BorderReady;
        _visibleFramePixels = state.VisibleFramePixels is null
            ? null
            : (byte[])state.VisibleFramePixels.Clone();
        _lastBootFramePixels = state.LastBootFramePixels is null
            ? null
            : (byte[])state.LastBootFramePixels.Clone();
        _borderCachePixels = null;
        Array.Clear(_borderOverlayMask);
        _borderCacheDirty = true;
    }

    internal LcdFrame ApplyPalettes(LcdFrame frame, byte maskMode)
    {
        var gameBoyPixels = maskMode switch
        {
            MaskFreeze => _visibleFramePixels ?? ColorizeFrame(frame),
            MaskBlack => CreateSolidRgb555Pixels(0x0000),
            MaskColor0 => CreateSolidRgb555Pixels(_palettes[0]),
            _ => SetVisibleRgb555Pixels(ColorizeFrame(frame)),
        };

        if (
            !_borderReady
            && IsSolidRgb555(gameBoyPixels, 0x7FFF)
            && _lastBootFramePixels is not null
        )
        {
            return CreateGameBoyFrame(_lastBootFramePixels);
        }

        if (!_borderReady && !IsSolidRgb555(gameBoyPixels, 0x7FFF))
        {
            _lastBootFramePixels = gameBoyPixels;
        }

        return _borderReady ? CreateSgbFrame(gameBoyPixels) : CreateGameBoyFrame(gameBoyPixels);
    }

    internal void ApplyPendingVramTransfer(ReadOnlySpan<byte> transferData, byte transfer)
    {
        switch (transfer)
        {
            case 1:
                for (var offset = 0; offset < transferData.Length; offset += sizeof(ushort))
                {
                    _systemPalettes[offset / sizeof(ushort)] =
                        BinaryPrimitives.ReadUInt16LittleEndian(transferData.Slice(offset));
                }

                break;
            case 2:
                transferData[..AttributeFileTransferSizeBytes].CopyTo(_attributeFiles);
                break;
            case 3:
                transferData[..SgbBorderTileTransferBytes].CopyTo(_borderTiles);
                break;
            case 4:
                transferData[..SgbBorderTileTransferBytes]
                    .CopyTo(_borderTiles.AsSpan(SgbBorderTileTransferBytes));
                break;
            case 5:
                for (var entry = 0; entry < SgbBorderMapEntries; entry++)
                {
                    _borderMap[entry] = BinaryPrimitives.ReadUInt16LittleEndian(
                        transferData.Slice(entry * sizeof(ushort))
                    );
                }

                for (var color = 0; color < SgbBorderPaletteColors; color++)
                {
                    _borderPalettes[color] = BinaryPrimitives.ReadUInt16LittleEndian(
                        transferData.Slice(SgbBorderPaletteOffset + (color * sizeof(ushort)))
                    );
                }

                _borderReady = true;
                break;
        }

        if (transfer is 3 or 4 or 5)
        {
            _borderCacheDirty = true;
        }
    }

    internal void SetPalettes(ReadOnlySpan<byte> command, int firstPalette, int secondPalette)
    {
        var sharedColor0 = BinaryPrimitives.ReadUInt16LittleEndian(command.Slice(1));
        _palettes[0] = sharedColor0;
        _palettes[4] = sharedColor0;
        _palettes[8] = sharedColor0;
        _palettes[12] = sharedColor0;

        for (var color = 1; color < 4; color++)
        {
            _palettes[(firstPalette * 4) + color] = BinaryPrimitives.ReadUInt16LittleEndian(
                command.Slice(3 + ((color - 1) * 2))
            );
            _palettes[(secondPalette * 4) + color] = BinaryPrimitives.ReadUInt16LittleEndian(
                command.Slice(9 + ((color - 1) * 2))
            );
        }

        _borderCacheDirty = true;
    }

    internal bool SetSystemPalettes(ReadOnlySpan<byte> command)
    {
        CopySystemPalette(command, commandOffset: 1, paletteIndex: 0);
        CopySystemPalette(command, commandOffset: 3, paletteIndex: 1);
        CopySystemPalette(command, commandOffset: 5, paletteIndex: 2);
        CopySystemPalette(command, commandOffset: 7, paletteIndex: 3);
        _palettes[4] = _palettes[0];
        _palettes[8] = _palettes[0];
        _palettes[12] = _palettes[0];
        _borderCacheDirty = true;

        if ((command[9] & 0x80) != 0)
        {
            ApplyAttributeFile(command[9] & 0x3F);
        }

        return (command[9] & 0x40) != 0;
    }

    internal bool SetAttributeFile(byte control)
    {
        ApplyAttributeFile(control & 0x3F);
        return (control & 0x40) != 0;
    }

    internal void SetBlockAttributes(ReadOnlySpan<byte> command)
    {
        var count = Math.Min((int)command[1], 18);
        for (var dataSet = 0; dataSet < count; dataSet++)
        {
            var offset = 2 + (dataSet * 6);
            if (offset + 5 >= command.Length)
            {
                return;
            }

            var control = command[offset];
            var paletteDesignations = command[offset + 1];
            var left = Math.Min(command[offset + 2] & 0x1F, AttributeMapWidth - 1);
            var top = Math.Min(command[offset + 3] & 0x1F, AttributeMapHeight - 1);
            var right = Math.Min(command[offset + 4] & 0x1F, AttributeMapWidth - 1);
            var bottom = Math.Min(command[offset + 5] & 0x1F, AttributeMapHeight - 1);
            if (left > right || top > bottom)
            {
                continue;
            }

            var inside = (control & 0x01) != 0;
            var border = (control & 0x02) != 0;
            var outside = (control & 0x04) != 0;
            var insidePalette = (byte)(paletteDesignations & 0x03);
            var borderPalette = (byte)((paletteDesignations >> 2) & 0x03);
            var outsidePalette = (byte)((paletteDesignations >> 4) & 0x03);
            if (inside && !border && !outside)
            {
                border = true;
                borderPalette = insidePalette;
            }
            else if (outside && !border && !inside)
            {
                border = true;
                borderPalette = outsidePalette;
            }

            for (var y = 0; y < AttributeMapHeight; y++)
            {
                for (var x = 0; x < AttributeMapWidth; x++)
                {
                    if (x < left || x > right || y < top || y > bottom)
                    {
                        if (outside)
                        {
                            SetAttribute(x, y, outsidePalette);
                        }
                    }
                    else if (x > left && x < right && y > top && y < bottom)
                    {
                        if (inside)
                        {
                            SetAttribute(x, y, insidePalette);
                        }
                    }
                    else if (border)
                    {
                        SetAttribute(x, y, borderPalette);
                    }
                }
            }
        }
    }

    internal void SetLineAttributes(ReadOnlySpan<byte> command)
    {
        var count = Math.Min(command[1], command.Length - 2);
        for (var offset = 2; offset < 2 + count; offset++)
        {
            var data = command[offset];
            var palette = (byte)((data >> 5) & 0x03);
            var line = data & 0x1F;
            if ((data & 0x80) == 0)
            {
                if (line >= AttributeMapWidth)
                {
                    continue;
                }

                for (var y = 0; y < AttributeMapHeight; y++)
                {
                    SetAttribute(line, y, palette);
                }
            }
            else
            {
                if (line >= AttributeMapHeight)
                {
                    continue;
                }

                for (var x = 0; x < AttributeMapWidth; x++)
                {
                    SetAttribute(x, line, palette);
                }
            }
        }
    }

    internal void SetDivisionAttributes(ReadOnlySpan<byte> command)
    {
        var paletteLow = (byte)(command[1] & 0x03);
        var paletteHigh = (byte)((command[1] >> 2) & 0x03);
        var paletteMiddle = (byte)((command[1] >> 4) & 0x03);
        var line = command[2] & 0x1F;
        var horizontal = (command[1] & 0x40) != 0;

        for (var y = 0; y < AttributeMapHeight; y++)
        {
            for (var x = 0; x < AttributeMapWidth; x++)
            {
                var position = horizontal ? y : x;
                byte palette;
                if (position < line)
                {
                    palette = paletteLow;
                }
                else if (position == line)
                {
                    palette = paletteMiddle;
                }
                else
                {
                    palette = paletteHigh;
                }

                SetAttribute(x, y, palette);
            }
        }
    }

    internal void SetCharacterAttributes(ReadOnlySpan<byte> command)
    {
        var x = command[1];
        var y = command[2];
        var count = BinaryPrimitives.ReadUInt16LittleEndian(command.Slice(3));
        var vertical = command[5] != 0;
        if (x >= AttributeMapWidth || y >= AttributeMapHeight)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var dataOffset = 6 + (index / 4);
            if (dataOffset >= command.Length)
            {
                return;
            }

            SetAttribute(x, y, (byte)((command[dataOffset] >> ((~index & 3) * 2)) & 0x03));
            if (vertical)
            {
                y++;
                if (y != AttributeMapHeight)
                {
                    continue;
                }

                x++;
                y = 0;
                if (x == AttributeMapWidth)
                {
                    return;
                }
            }
            else
            {
                x++;
                if (x != AttributeMapWidth)
                {
                    continue;
                }

                y++;
                x = 0;
                if (y == AttributeMapHeight)
                {
                    return;
                }
            }
        }
    }

    private byte[] ColorizeFrame(LcdFrame frame)
    {
        var source = frame.Pixels.Span;
        var target = _borderReady
            ? _borderGameBoyPixels ??= new byte[
                PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
            ]
            : new byte[PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel];

        for (var pixel = 0; pixel < source.Length; pixel++)
        {
            var x = pixel % PpuGeometry.FrameWidth;
            var y = pixel / PpuGeometry.FrameWidth;
            var palette = _attributeMap[(x / 8) + (y / 8 * AttributeMapWidth)];
            WriteRgb555(target, pixel, _palettes[(palette * 4) + (source[pixel] & 0x03)]);
        }

        return target;
    }

    private void CopySystemPalette(ReadOnlySpan<byte> command, int commandOffset, int paletteIndex)
    {
        var systemPaletteId = command[commandOffset] | ((command[commandOffset + 1] & 0x01) << 8);
        var sourceOffset = systemPaletteId * 4;
        var targetOffset = paletteIndex * 4;
        for (var color = 0; color < 4; color++)
        {
            _palettes[targetOffset + color] = _systemPalettes[sourceOffset + color];
        }
    }

    private void ApplyAttributeFile(int fileIndex)
    {
        if (fileIndex >= AttributeFileCount)
        {
            return;
        }

        var sourceOffset = fileIndex * AttributeFilePackedSize;
        for (var y = 0; y < AttributeMapHeight; y++)
        {
            for (var group = 0; group < AttributeMapWidth / 4; group++)
            {
                var packedAttributes = _attributeFiles[sourceOffset + (y * 5) + group];
                for (var xInGroup = 0; xInGroup < 4; xInGroup++)
                {
                    SetAttribute(
                        (group * 4) + xInGroup,
                        y,
                        (byte)((packedAttributes >> ((3 - xInGroup) * 2)) & 0x03)
                    );
                }
            }
        }
    }

    private void SetAttribute(int x, int y, byte palette)
    {
        _attributeMap[x + (y * AttributeMapWidth)] = palette;
    }

    private static bool IsSolidRgb555(ReadOnlySpan<byte> pixels, ushort color)
    {
        for (var offset = 0; offset < pixels.Length; offset += Rgb555BytesPerPixel)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(pixels.Slice(offset)) != color)
            {
                return false;
            }
        }

        return true;
    }

    private byte[] SetVisibleRgb555Pixels(byte[] pixels)
    {
        _visibleFramePixels = pixels;
        return pixels;
    }

    private static byte[] CreateSolidRgb555Pixels(ushort color)
    {
        var pixels = new byte[
            PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
        ];
        for (var pixel = 0; pixel < PpuGeometry.FrameWidth * PpuGeometry.FrameHeight; pixel++)
        {
            WriteRgb555(pixels, pixel, color);
        }

        return pixels;
    }

    private LcdFrame CreateSgbFrame(ReadOnlySpan<byte> gameBoyPixels)
    {
        const int pixelLength = SgbScreenPixelCount * Rgb555BytesPerPixel;

        var borderPixels = GetBorderCachePixels();
        var pixels = ArrayPool<byte>.Shared.Rent(pixelLength);

        try
        {
            borderPixels.CopyTo(pixels, 0);
            CopyGameBoyFrame(pixels, gameBoyPixels);
            CopyBorderOverlay(pixels, borderPixels);

            return LcdFrame.FromPooledPixels(
                SgbScreenWidth,
                SgbScreenHeight,
                LcdPixelFormat.Rgb555Le,
                ArrayPool<byte>.Shared,
                pixels,
                pixelLength
            );
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(pixels);
            throw;
        }
    }

    private static LcdFrame CreateGameBoyFrame(byte[] gameBoyPixels) =>
        LcdFrame.FromOwnedPixels(
            PpuGeometry.FrameWidth,
            PpuGeometry.FrameHeight,
            LcdPixelFormat.Rgb555Le,
            gameBoyPixels
        );

    private byte[] GetBorderCachePixels()
    {
        var pixels = _borderCachePixels ??= new byte[SgbScreenPixelCount * Rgb555BytesPerPixel];
        if (!_borderCacheDirty)
        {
            return pixels;
        }

        for (var pixel = 0; pixel < SgbScreenPixelCount; pixel++)
        {
            WriteRgb555(pixels, pixel, _palettes[0]);
        }

        Array.Clear(_borderOverlayMask);
        RenderBorder(pixels);
        _borderCacheDirty = false;
        return pixels;
    }

    private void CopyBorderOverlay(Span<byte> target, ReadOnlySpan<byte> borderPixels)
    {
        for (var y = 0; y < PpuGeometry.FrameHeight; y++)
        {
            var gameBoyRow = y * PpuGeometry.FrameWidth;
            var screenRow = (SgbGameBoyY + y) * SgbScreenWidth;
            for (var x = 0; x < PpuGeometry.FrameWidth; x++)
            {
                var gameBoyPixel = gameBoyRow + x;
                if ((_borderOverlayMask[gameBoyPixel >> 3] & (1 << (gameBoyPixel & 7))) == 0)
                {
                    continue;
                }

                var offset = (screenRow + SgbGameBoyX + x) * Rgb555BytesPerPixel;
                target[offset] = borderPixels[offset];
                target[offset + 1] = borderPixels[offset + 1];
            }
        }
    }

    private static void CopyGameBoyFrame(Span<byte> target, ReadOnlySpan<byte> source)
    {
        for (var y = 0; y < PpuGeometry.FrameHeight; y++)
        {
            source
                .Slice(
                    y * PpuGeometry.FrameWidth * Rgb555BytesPerPixel,
                    PpuGeometry.FrameWidth * Rgb555BytesPerPixel
                )
                .CopyTo(
                    target.Slice(
                        (((SgbGameBoyY + y) * SgbScreenWidth) + SgbGameBoyX) * Rgb555BytesPerPixel,
                        PpuGeometry.FrameWidth * Rgb555BytesPerPixel
                    )
                );
        }
    }

    private void RenderBorder(Span<byte> pixels)
    {
        for (var tileY = 0; tileY < SgbBorderMapHeight; tileY++)
        {
            for (var tileX = 0; tileX < SgbBorderMapWidth; tileX++)
            {
                RenderBorderTile(pixels, tileX, tileY);
            }
        }
    }

    private void RenderBorderTile(Span<byte> pixels, int tileX, int tileY)
    {
        var tile = _borderMap[tileX + (tileY * SgbBorderMapWidth)];
        if ((tile & 0x0300) != 0)
        {
            return;
        }

        var tileBase = (tile & 0x00FF) * SgbBorderTileBytes;
        var paletteBase = ((tile >> 10) & 0x03) * 16;
        var coversGameBoyArea = tileX is >= 6 and < 26 && tileY is >= 5 and < 23;
        var yFlip = (tile & 0x8000) != 0;
        var xFlip = (tile & 0x4000) != 0;

        for (var y = 0; y < 8; y++)
        {
            var sourceY = yFlip ? 7 - y : y;
            var rowOffset = tileBase + (sourceY * 2);
            for (var x = 0; x < 8; x++)
            {
                var bit = 1 << (xFlip ? x : 7 - x);
                var color =
                    ((_borderTiles[rowOffset] & bit) == 0 ? 0 : 1)
                    | ((_borderTiles[rowOffset + 1] & bit) == 0 ? 0 : 2)
                    | ((_borderTiles[rowOffset + 16] & bit) == 0 ? 0 : 4)
                    | ((_borderTiles[rowOffset + 17] & bit) == 0 ? 0 : 8);
                if (color == 0 && coversGameBoyArea)
                {
                    continue;
                }

                var pixel = (((tileY * 8) + y) * SgbScreenWidth) + (tileX * 8) + x;
                WriteRgb555(
                    pixels,
                    pixel,
                    color == 0 ? _palettes[0] : _borderPalettes[paletteBase + color]
                );
                if (coversGameBoyArea)
                {
                    var gameBoyPixel =
                        (((tileY * 8) + y - SgbGameBoyY) * PpuGeometry.FrameWidth)
                        + ((tileX * 8) + x - SgbGameBoyX);
                    _borderOverlayMask[gameBoyPixel >> 3] |= (byte)(1 << (gameBoyPixel & 7));
                }
            }
        }
    }

    private static void WriteRgb555(Span<byte> pixels, int pixelIndex, ushort color) =>
        BinaryPrimitives.WriteUInt16LittleEndian(
            pixels.Slice(pixelIndex * Rgb555BytesPerPixel),
            color
        );
}

internal readonly record struct SgbBorderRendererState(
    ushort[] SystemPalettes,
    byte[] AttributeFiles,
    byte[] BorderTiles,
    ushort[] BorderMap,
    ushort[] BorderPalettes,
    ushort[] Palettes,
    byte[] AttributeMap,
    bool BorderReady,
    byte[]? VisibleFramePixels,
    byte[]? LastBootFramePixels
);
