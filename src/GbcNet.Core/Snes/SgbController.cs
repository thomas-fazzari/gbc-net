// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Ppu;

namespace GbcNet.Core.Snes;

/// <summary>
/// Receives high-level SGB command packets through JOYP and tracks SNES-side state.
/// </summary>
internal sealed class SgbController(bool commandsEnabled)
{
    private const int PacketSizeBytes = 16;
    private const int MaxPacketCount = 7;
    private const int MaxCommandSizeBytes = PacketSizeBytes * MaxPacketCount;
    private const byte SelectBitsMask = 0x30;
    private const byte P15Bit = 0x20;
    private const byte Pal01Command = 0x00;
    private const byte Pal23Command = 0x01;
    private const byte Pal03Command = 0x02;
    private const byte Pal12Command = 0x03;
    private const byte AttrBlkCommand = 0x04;
    private const byte AttrLinCommand = 0x05;
    private const byte AttrDivCommand = 0x06;
    private const byte AttrChrCommand = 0x07;
    private const byte PalSetCommand = 0x0A;
    private const byte PalTrnCommand = 0x0B;
    private const byte DataSndCommand = 0x0F;
    private const byte MltReqCommand = 0x11;
    private const byte ChrTrnCommand = 0x13;
    private const byte PctTrnCommand = 0x14;
    private const byte AttrTrnCommand = 0x15;
    private const byte AttrSetCommand = 0x16;
    private const byte MaskEnCommand = 0x17;
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
    private const int PaletteTransferTileCount = 0x100;
    private const int BorderDataTransferTileCount = 0x88;
    private const int AttributeTransferTileCount = 0xFE;
    private const byte VramTransferFrameDelay = 3;
    private const int AttributeMapWidth = 20;
    private const int AttributeMapHeight = 18;
    private const int AttributeFilePackedSize = 90;
    private const int AttributeFileCount = 45;
    private const int AttributeFileTransferSizeBytes = AttributeFilePackedSize * AttributeFileCount;
    private const int Rgb555BytesPerPixel = 2;
    private const int SgbScreenPixelCount = SgbScreenWidth * SgbScreenHeight;
    private const int BorderOverlayMaskSizeBytes =
        ((PpuGeometry.FrameWidth * PpuGeometry.FrameHeight) + 7) / 8;
    private const int VramTransferSizeBytes = 4096;
    private const byte NoPendingVramTransfer = 0;
    private const byte PendingPaletteTransfer = 1;
    private const byte PendingAttributeTransfer = 2;
    private const byte PendingBorderTileLowTransfer = 3;
    private const byte PendingBorderTileHighTransfer = 4;
    private const byte PendingBorderMapTransfer = 5;
    private const byte MaskFreeze = 1;
    private const byte MaskBlack = 2;
    private const byte MaskColor0 = 3;

    private readonly byte[] _command = new byte[MaxCommandSizeBytes];
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
    private int _commandWriteBitIndex;
    private bool _readyForPulse;
    private bool _readyForWrite;
    private bool _readyForStop;
    private int _playerCount = 1;
    private int _currentPlayer;
    private byte _maskMode;
    private byte _pendingVramTransfer;
    private byte _pendingVramTransferFrameDelay;
    private bool _borderReady;
    private byte[]? _borderCachePixels;
    private bool _borderCacheDirty = true;
    private byte[]? _visibleFramePixels;
    private byte[]? _lastBootFramePixels;

    public bool HasPendingVramTransfer => _pendingVramTransfer != NoPendingVramTransfer;

    internal SgbControllerState CaptureState() =>
        new(
            (byte[])_command.Clone(),
            (ushort[])_systemPalettes.Clone(),
            (byte[])_attributeFiles.Clone(),
            (byte[])_borderTiles.Clone(),
            (ushort[])_borderMap.Clone(),
            (ushort[])_borderPalettes.Clone(),
            (ushort[])_palettes.Clone(),
            (byte[])_attributeMap.Clone(),
            _commandWriteBitIndex,
            _readyForPulse,
            _readyForWrite,
            _readyForStop,
            _playerCount,
            _currentPlayer,
            _maskMode,
            _pendingVramTransfer,
            _pendingVramTransferFrameDelay,
            _borderReady,
            _visibleFramePixels is null ? null : (byte[])_visibleFramePixels.Clone(),
            _lastBootFramePixels is null ? null : (byte[])_lastBootFramePixels.Clone()
        );

    internal void ValidateState(SgbControllerState state)
    {
        if (
            state.Command is null
            || state.Command.Length != _command.Length
            || state.SystemPalettes is null
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
            throw new ArgumentException("SGB state has an invalid buffer shape.", nameof(state));
        }

        var invalidPlayer =
            state.PlayerCount is not (1 or 2 or 4)
            || state.CurrentPlayer < 0
            || state.CurrentPlayer >= state.PlayerCount;

        var invalidVramTransfer = state.PendingVramTransfer switch
        {
            > PendingBorderMapTransfer => true,
            NoPendingVramTransfer => state.PendingVramTransferFrameDelay != 0,
            _ => state.PendingVramTransferFrameDelay is < 1 or > VramTransferFrameDelay,
        };

        if (
            state.CommandWriteBitIndex is < 0 or > MaxCommandSizeBytes * 8
            || invalidPlayer
            || state.MaskMode > MaskColor0
            || invalidVramTransfer
            || state.AttributeMap.Any(static attribute => attribute > 3)
        )
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal void RestoreState(SgbControllerState state)
    {
        ValidateState(state);

        state.Command.CopyTo(_command, 0);
        state.SystemPalettes.CopyTo(_systemPalettes, 0);
        state.AttributeFiles.CopyTo(_attributeFiles, 0);
        state.BorderTiles.CopyTo(_borderTiles, 0);
        state.BorderMap.CopyTo(_borderMap, 0);
        state.BorderPalettes.CopyTo(_borderPalettes, 0);
        state.Palettes.CopyTo(_palettes, 0);
        state.AttributeMap.CopyTo(_attributeMap, 0);
        _visibleFramePixels = state.VisibleFramePixels is null
            ? null
            : (byte[])state.VisibleFramePixels.Clone();
        _lastBootFramePixels = state.LastBootFramePixels is null
            ? null
            : (byte[])state.LastBootFramePixels.Clone();
        _commandWriteBitIndex = state.CommandWriteBitIndex;
        _readyForPulse = state.ReadyForPulse;
        _readyForWrite = state.ReadyForWrite;
        _readyForStop = state.ReadyForStop;
        _playerCount = state.PlayerCount;
        _currentPlayer = state.CurrentPlayer;
        _maskMode = state.MaskMode;
        _pendingVramTransfer = state.PendingVramTransfer;
        _pendingVramTransferFrameDelay = state.PendingVramTransferFrameDelay;
        _borderReady = state.BorderReady;
        _borderCachePixels = null;
        Array.Clear(_borderOverlayMask);
        _borderCacheDirty = true;
    }

    public void Write(byte value, byte previousSelectedGroups)
    {
        var selectedGroups = (byte)(value & SelectBitsMask);
        if (
            _playerCount > 1
            && (previousSelectedGroups & P15Bit) == 0
            && (selectedGroups & P15Bit) != 0
        )
        {
            _currentPlayer = (_currentPlayer + 1) & (_playerCount - 1);
        }

        if (!commandsEnabled)
        {
            return;
        }

        var commandSizeBits = GetCommandSizeBits();
        switch (selectedGroups >> 4)
        {
            case 0b11:
                _readyForPulse = true;
                return;

            case 0b10:
                ReceiveBit(value: 0, commandSizeBits);
                return;

            case 0b01:
                ReceiveBit(value: 1, commandSizeBits);
                return;

            case 0b00:
                PreparePacketWrite();
                return;
        }
    }

    public byte ReadLowNibble(byte selectedGroups, byte lowNibble)
    {
        return selectedGroups == SelectBitsMask && _playerCount > 1
            ? (byte)(0x0F - _currentPlayer)
            : lowNibble;
    }

    public LcdFrame ApplyPalettes(LcdFrame frame)
    {
        if (frame.PixelFormat is not LcdPixelFormat.DmgShadeIndex8)
        {
            return frame;
        }

        var colorizedPixels = ColorizeFrame(frame);
        var gameBoyPixels = _maskMode switch
        {
            MaskFreeze => _visibleFramePixels ?? colorizedPixels,
            MaskBlack => CreateSolidRgb555Pixels(0x0000, colorizedPixels),
            MaskColor0 => CreateSolidRgb555Pixels(_palettes[0], colorizedPixels),
            _ => SetVisibleRgb555Pixels(colorizedPixels),
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

    public void ApplyPendingVramTransfer(ReadOnlySpan<byte> transferData)
    {
        if (_pendingVramTransfer == NoPendingVramTransfer)
        {
            return;
        }

        if (transferData.Length < VramTransferSizeBytes)
        {
            throw new ArgumentException(
                "SGB VRAM transfer data must be 4096 bytes.",
                nameof(transferData)
            );
        }

        switch (_pendingVramTransfer)
        {
            case PendingPaletteTransfer:
                for (var offset = 0; offset < VramTransferSizeBytes; offset += 2)
                {
                    _systemPalettes[offset / 2] = ReadUInt16(transferData, offset);
                }

                break;

            case PendingAttributeTransfer:
                transferData[..AttributeFileTransferSizeBytes].CopyTo(_attributeFiles);
                break;

            case PendingBorderTileLowTransfer:
                transferData[..SgbBorderTileTransferBytes].CopyTo(_borderTiles);
                break;

            case PendingBorderTileHighTransfer:
                transferData[..SgbBorderTileTransferBytes]
                    .CopyTo(_borderTiles.AsSpan(SgbBorderTileTransferBytes));
                break;

            case PendingBorderMapTransfer:
                for (var entry = 0; entry < SgbBorderMapEntries; entry++)
                {
                    _borderMap[entry] = ReadUInt16(transferData, entry * 2);
                }

                for (var color = 0; color < SgbBorderPaletteColors; color++)
                {
                    _borderPalettes[color] = ReadUInt16(
                        transferData,
                        SgbBorderPaletteOffset + (color * 2)
                    );
                }

                _borderReady = true;
                break;
        }

        if (
            _pendingVramTransfer
            is PendingBorderTileLowTransfer
                or PendingBorderTileHighTransfer
                or PendingBorderMapTransfer
        )
        {
            _borderCacheDirty = true;
        }

        _pendingVramTransfer = NoPendingVramTransfer;
        _pendingVramTransferFrameDelay = 0;
    }

    public void ApplyPendingVramTransfer(LcdFrame transferFrame)
    {
        if (_pendingVramTransfer == NoPendingVramTransfer)
        {
            return;
        }

        if (transferFrame.PixelFormat is not LcdPixelFormat.DmgShadeIndex8)
        {
            throw new ArgumentException(
                "SGB VRAM transfer frame must contain DMG shade pixels.",
                nameof(transferFrame)
            );
        }

        if (_pendingVramTransferFrameDelay > 0 && --_pendingVramTransferFrameDelay > 0)
        {
            return;
        }

        var transferData = new byte[VramTransferSizeBytes];
        DecodeTransferFrame(transferFrame.Pixels.Span, GetPendingTransferTileCount(), transferData);
        ApplyPendingVramTransfer(transferData);
    }

    private int GetCommandSizeBits()
    {
        var packetCount = IsSupportedCommand(_command[0] >> 3) ? _command[0] & 0x07 : 1;

        if (packetCount == 0)
        {
            packetCount = 1;
        }

        return packetCount * PacketSizeBytes * 8;
    }

    private static bool IsSupportedCommand(int command) =>
        command
            is Pal01Command
                or Pal23Command
                or Pal03Command
                or Pal12Command
                or AttrBlkCommand
                or AttrLinCommand
                or AttrDivCommand
                or AttrChrCommand
                or PalSetCommand
                or PalTrnCommand
                or DataSndCommand
                or MltReqCommand
                or ChrTrnCommand
                or PctTrnCommand
                or AttrTrnCommand
                or AttrSetCommand
                or MaskEnCommand;

    private void PreparePacketWrite()
    {
        _readyForWrite = true;
        _readyForPulse = false;

        if (
            (_commandWriteBitIndex & ((PacketSizeBytes * 8) - 1)) == 0
            && _commandWriteBitIndex != 0
            && !_readyForStop
        )
        {
            return;
        }

        ClearCommand();
        _readyForStop = false;
    }

    private void ReceiveBit(byte value, int commandSizeBits)
    {
        if (!_readyForPulse || !_readyForWrite)
        {
            return;
        }

        if (_readyForStop)
        {
            if (value == 0 && _commandWriteBitIndex == commandSizeBits)
            {
                ExecuteCommand();
                ClearCommand();
            }

            _readyForPulse = false;
            _readyForWrite = false;
            _readyForStop = false;
            return;
        }

        if (_commandWriteBitIndex >= MaxCommandSizeBytes * 8)
        {
            return;
        }

        if (value != 0)
        {
            _command[_commandWriteBitIndex / 8] |= (byte)(1 << (_commandWriteBitIndex & 7));
        }

        _commandWriteBitIndex++;
        _readyForPulse = false;

        if ((_commandWriteBitIndex & ((PacketSizeBytes * 8) - 1)) == 0)
        {
            _readyForStop = true;
        }
    }

    private void ExecuteCommand()
    {
        if ((_command[0] & 0x07) == 0)
        {
            return;
        }

        var command = _command[0] >> 3;
        if (!IsSupportedCommand(command))
        {
            return;
        }

        switch (command)
        {
            case Pal01Command:
                SetPalettes(firstPalette: 0, secondPalette: 1);
                return;
            case Pal23Command:
                SetPalettes(firstPalette: 2, secondPalette: 3);
                return;
            case Pal03Command:
                SetPalettes(firstPalette: 0, secondPalette: 3);
                return;
            case Pal12Command:
                SetPalettes(firstPalette: 1, secondPalette: 2);
                return;
            case AttrBlkCommand:
                SetBlockAttributes();
                return;
            case AttrLinCommand:
                SetLineAttributes();
                return;
            case AttrDivCommand:
                SetDivisionAttributes();
                return;
            case AttrChrCommand:
                SetCharacterAttributes();
                return;
            case PalSetCommand:
                SetSystemPalettes();
                return;
            case PalTrnCommand:
                RequestVramTransfer(PendingPaletteTransfer);
                return;
            case DataSndCommand:
                // DATA_SND writes SNES WRAM for SGB firmware hot patches
                // This HLE path does not execute SNES code.
                return;
            case MltReqCommand:
                SetPlayerCount(_command[1] & 0x03);
                return;
            case ChrTrnCommand:
                RequestVramTransfer(
                    (_command[1] & 0x01) == 0
                        ? PendingBorderTileLowTransfer
                        : PendingBorderTileHighTransfer
                );
                return;
            case PctTrnCommand:
                RequestVramTransfer(PendingBorderMapTransfer);
                return;
            case AttrTrnCommand:
                RequestVramTransfer(PendingAttributeTransfer);
                return;
            case AttrSetCommand:
                SetAttributeFile(_command[1]);
                return;
            case MaskEnCommand:
                _maskMode = (byte)(_command[1] & 0x03);
                return;
        }
    }

    private byte[] ColorizeFrame(LcdFrame frame)
    {
        var source = frame.Pixels.Span;
        var target = new byte[
            PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
        ];

        for (var pixel = 0; pixel < source.Length; pixel++)
        {
            var x = pixel % PpuGeometry.FrameWidth;
            var y = pixel / PpuGeometry.FrameWidth;
            var palette = _attributeMap[(x / 8) + (y / 8 * AttributeMapWidth)];
            WriteRgb555(target, pixel, _palettes[(palette * 4) + (source[pixel] & 0x03)]);
        }

        return target;
    }

    private void RequestVramTransfer(byte transfer)
    {
        _pendingVramTransfer = transfer;
        _pendingVramTransferFrameDelay = VramTransferFrameDelay;
    }

    private int GetPendingTransferTileCount() =>
        _pendingVramTransfer switch
        {
            PendingPaletteTransfer
            or PendingBorderTileLowTransfer
            or PendingBorderTileHighTransfer => PaletteTransferTileCount,
            PendingBorderMapTransfer => BorderDataTransferTileCount,
            PendingAttributeTransfer => AttributeTransferTileCount,
            _ => 0,
        };

    private static void DecodeTransferFrame(
        ReadOnlySpan<byte> shades,
        int tileCount,
        Span<byte> transferData
    )
    {
        for (var tile = 0; tile < tileCount; tile++)
        {
            var tileX = tile % AttributeMapWidth * 8;
            var tileY = tile / AttributeMapWidth * 8;
            var targetOffset = tile * 16;

            for (var y = 0; y < 8; y++)
            {
                byte low = 0;
                byte high = 0;
                for (var x = 0; x < 8; x++)
                {
                    var shade = shades[tileX + x + ((tileY + y) * PpuGeometry.FrameWidth)] & 0x03;
                    var bit = (byte)(0x80 >> x);
                    if ((shade & 0x01) != 0)
                    {
                        low |= bit;
                    }

                    if ((shade & 0x02) != 0)
                    {
                        high |= bit;
                    }
                }

                transferData[targetOffset + (y * 2)] = low;
                transferData[targetOffset + (y * 2) + 1] = high;
            }
        }
    }

    private static bool IsSolidRgb555(ReadOnlySpan<byte> pixels, ushort color)
    {
        for (var offset = 0; offset < pixels.Length; offset += Rgb555BytesPerPixel)
        {
            if ((ushort)(pixels[offset] | (pixels[offset + 1] << 8)) != color)
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

    private byte[] CreateSolidRgb555Pixels(ushort color, byte[] visiblePixels)
    {
        _visibleFramePixels = visiblePixels;
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
        var borderPixels = GetBorderCachePixels();
        var pixels = (byte[])borderPixels.Clone();

        CopyGameBoyFrame(pixels, gameBoyPixels);
        CopyBorderOverlay(pixels, borderPixels);

        return LcdFrame.FromOwnedPixels(
            SgbScreenWidth,
            SgbScreenHeight,
            LcdPixelFormat.Rgb555Le,
            pixels
        );
    }

    private static LcdFrame CreateGameBoyFrame(ReadOnlySpan<byte> gameBoyPixels) =>
        new(
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

    private void SetPalettes(int firstPalette, int secondPalette)
    {
        var sharedColor0 = ReadUInt16(1);
        _palettes[0] = sharedColor0;
        _palettes[4] = sharedColor0;
        _palettes[8] = sharedColor0;
        _palettes[12] = sharedColor0;

        for (var color = 1; color < 4; color++)
        {
            _palettes[(firstPalette * 4) + color] = ReadUInt16(3 + ((color - 1) * 2));
            _palettes[(secondPalette * 4) + color] = ReadUInt16(9 + ((color - 1) * 2));
        }

        _borderCacheDirty = true;
    }

    private void SetSystemPalettes()
    {
        CopySystemPalette(commandOffset: 1, paletteIndex: 0);
        CopySystemPalette(commandOffset: 3, paletteIndex: 1);
        CopySystemPalette(commandOffset: 5, paletteIndex: 2);
        CopySystemPalette(commandOffset: 7, paletteIndex: 3);
        _palettes[4] = _palettes[0];
        _palettes[8] = _palettes[0];
        _palettes[12] = _palettes[0];
        _borderCacheDirty = true;

        if ((_command[9] & 0x80) != 0)
        {
            ApplyAttributeFile(_command[9] & 0x3F);
        }

        if ((_command[9] & 0x40) != 0)
        {
            _maskMode = 0;
        }
    }

    private void CopySystemPalette(int commandOffset, int paletteIndex)
    {
        var systemPaletteId = _command[commandOffset] | ((_command[commandOffset + 1] & 0x01) << 8);
        var sourceOffset = systemPaletteId * 4;
        var targetOffset = paletteIndex * 4;
        for (var color = 0; color < 4; color++)
        {
            _palettes[targetOffset + color] = _systemPalettes[sourceOffset + color];
        }
    }

    private void SetAttributeFile(byte control)
    {
        ApplyAttributeFile(control & 0x3F);
        if ((control & 0x40) != 0)
        {
            _maskMode = 0;
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

    private void SetBlockAttributes()
    {
        var count = Math.Min((int)_command[1], 18);
        for (var dataSet = 0; dataSet < count; dataSet++)
        {
            var offset = 2 + (dataSet * 6);
            if (offset + 5 >= _command.Length)
            {
                return;
            }

            var control = _command[offset];
            var paletteDesignations = _command[offset + 1];
            var left = Math.Min(_command[offset + 2] & 0x1F, AttributeMapWidth - 1);
            var top = Math.Min(_command[offset + 3] & 0x1F, AttributeMapHeight - 1);
            var right = Math.Min(_command[offset + 4] & 0x1F, AttributeMapWidth - 1);
            var bottom = Math.Min(_command[offset + 5] & 0x1F, AttributeMapHeight - 1);
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

    private void SetLineAttributes()
    {
        var count = Math.Min(_command[1], _command.Length - 2);
        for (var offset = 2; offset < 2 + count; offset++)
        {
            var data = _command[offset];
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

    private void SetDivisionAttributes()
    {
        var paletteLow = (byte)(_command[1] & 0x03);
        var paletteHigh = (byte)((_command[1] >> 2) & 0x03);
        var paletteMiddle = (byte)((_command[1] >> 4) & 0x03);
        var line = _command[2] & 0x1F;
        var horizontal = (_command[1] & 0x40) != 0;

        for (var y = 0; y < AttributeMapHeight; y++)
        {
            for (var x = 0; x < AttributeMapWidth; x++)
            {
                var position = horizontal ? y : x;
                var palette = paletteHigh;
                if (position < line)
                {
                    palette = paletteLow;
                }
                else if (position == line)
                {
                    palette = paletteMiddle;
                }

                SetAttribute(x, y, palette);
            }
        }
    }

    private void SetCharacterAttributes()
    {
        var x = _command[1];
        var y = _command[2];
        var count = ReadUInt16(3);
        var vertical = _command[5] != 0;
        if (x >= AttributeMapWidth || y >= AttributeMapHeight)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var dataOffset = 6 + (index / 4);
            if (dataOffset >= _command.Length)
            {
                return;
            }

            SetAttribute(x, y, (byte)((_command[dataOffset] >> ((~index & 3) * 2)) & 0x03));
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

    private void SetPlayerCount(int mode)
    {
        _playerCount = mode switch
        {
            1 => 2,
            3 => 4,
            _ => 1,
        };
        _currentPlayer &= _playerCount - 1;
    }

    private void ClearCommand()
    {
        Array.Clear(_command);
        _commandWriteBitIndex = 0;
    }

    private void SetAttribute(int x, int y, byte palette)
    {
        _attributeMap[x + (y * AttributeMapWidth)] = palette;
    }

    private ushort ReadUInt16(int offset) =>
        (ushort)(_command[offset] | (_command[offset + 1] << 8));

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

    private static void WriteRgb555(Span<byte> pixels, int pixelIndex, ushort color)
    {
        var offset = pixelIndex * Rgb555BytesPerPixel;
        pixels[offset] = (byte)color;
        pixels[offset + 1] = (byte)(color >> 8);
    }
}

internal readonly record struct SgbControllerState(
    byte[] Command,
    ushort[] SystemPalettes,
    byte[] AttributeFiles,
    byte[] BorderTiles,
    ushort[] BorderMap,
    ushort[] BorderPalettes,
    ushort[] Palettes,
    byte[] AttributeMap,
    int CommandWriteBitIndex,
    bool ReadyForPulse,
    bool ReadyForWrite,
    bool ReadyForStop,
    int PlayerCount,
    int CurrentPlayer,
    byte MaskMode,
    byte PendingVramTransfer,
    byte PendingVramTransferFrameDelay,
    bool BorderReady,
    byte[]? VisibleFramePixels,
    byte[]? LastBootFramePixels
);
