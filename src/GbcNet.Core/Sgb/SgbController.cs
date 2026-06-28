using GbcNet.Core.Ppu;

namespace GbcNet.Core.Sgb;

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
    private const byte MltReqCommand = 0x11;
    private const byte MaskEnCommand = 0x17;
    private const int AttributeMapWidth = 20;
    private const int AttributeMapHeight = 18;
    private const int Rgb555BytesPerPixel = 2;
    private const int VramTransferSizeBytes = 4096;
    private const byte MaskFreeze = 1;
    private const byte MaskBlack = 2;
    private const byte MaskColor0 = 3;

    private readonly byte[] _command = new byte[MaxCommandSizeBytes];
    private readonly ushort[] _systemPalettes = new ushort[512 * 4];
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
    private bool _pendingPaletteTransfer;
    private byte[]? _visibleFramePixels;

    public bool HasPendingVramTransfer => _pendingPaletteTransfer;

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
        return _maskMode switch
        {
            MaskFreeze => CreateRgb555Frame(_visibleFramePixels ?? colorizedPixels),
            MaskBlack => CreateSolidRgb555Frame(0x0000, colorizedPixels),
            MaskColor0 => CreateSolidRgb555Frame(_palettes[0], colorizedPixels),
            _ => CreateVisibleRgb555Frame(colorizedPixels),
        };
    }

    public void ApplyPendingVramTransfer(ReadOnlySpan<byte> transferData)
    {
        if (!_pendingPaletteTransfer)
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

        for (var offset = 0; offset < VramTransferSizeBytes; offset += 2)
        {
            _systemPalettes[offset / 2] = ReadUInt16(transferData, offset);
        }

        _pendingPaletteTransfer = false;
    }

    private int GetCommandSizeBits()
    {
        var packetCount = _command[0] & 0x07;
        if (packetCount == 0)
        {
            packetCount = 1;
        }

        return packetCount * PacketSizeBytes * 8;
    }

    private void PreparePacketWrite()
    {
        if (!_readyForPulse)
        {
            return;
        }

        _readyForWrite = true;
        _readyForPulse = false;

        if (
            (_commandWriteBitIndex & ((PacketSizeBytes * 8) - 1)) != 0
            || _commandWriteBitIndex == 0
            || _readyForStop
        )
        {
            ClearCommand();
            _readyForStop = false;
        }
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

        if (_commandWriteBitIndex < MaxCommandSizeBytes * 8)
        {
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
    }

    private void ExecuteCommand()
    {
        if ((_command[0] & 0x07) == 0)
        {
            return;
        }

        switch (_command[0] >> 3)
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
                _pendingPaletteTransfer = true;
                return;
            case MltReqCommand:
                SetPlayerCount(_command[1] & 0x03);
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
            var palette = _attributeMap[(x / 8) + ((y / 8) * AttributeMapWidth)];
            WriteRgb555(target, pixel, _palettes[(palette * 4) + (source[pixel] & 0x03)]);
        }

        return target;
    }

    private LcdFrame CreateVisibleRgb555Frame(byte[] pixels)
    {
        _visibleFramePixels = pixels;
        return CreateRgb555Frame(pixels);
    }

    private LcdFrame CreateSolidRgb555Frame(ushort color, byte[] visiblePixels)
    {
        _visibleFramePixels = visiblePixels;
        var pixels = new byte[
            PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
        ];
        for (var pixel = 0; pixel < PpuGeometry.FrameWidth * PpuGeometry.FrameHeight; pixel++)
        {
            WriteRgb555(pixels, pixel, color);
        }

        return CreateRgb555Frame(pixels);
    }

    private static LcdFrame CreateRgb555Frame(ReadOnlySpan<byte> pixels) =>
        new(PpuGeometry.FrameWidth, PpuGeometry.FrameHeight, LcdPixelFormat.Rgb555Le, pixels);

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
