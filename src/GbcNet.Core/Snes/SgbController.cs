// Copyright (C) 2026 GBC.Net Contributors
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

    /// <summary>
    /// DATA_SND writes SNES WRAM for SGB firmware hot patches.
    /// This HLE path does not execute SNES code.
    /// </summary>
    private const byte DataSndCommand = 0x0F;
    private const byte MltReqCommand = 0x11;
    private const byte ChrTrnCommand = 0x13;
    private const byte PctTrnCommand = 0x14;
    private const byte AttrTrnCommand = 0x15;
    private const byte AttrSetCommand = 0x16;
    private const byte MaskEnCommand = 0x17;
    private const int PaletteTransferTileCount = 0x100;
    private const int BorderDataTransferTileCount = 0x88;
    private const int AttributeTransferTileCount = 0xFE;
    private const byte VramTransferFrameDelay = 3;
    private const int AttributeMapWidth = 20;
    private const int VramTransferSizeBytes = 4096;
    private const byte NoPendingVramTransfer = 0;
    private const byte PendingPaletteTransfer = 1;
    private const byte PendingAttributeTransfer = 2;
    private const byte PendingBorderTileLowTransfer = 3;
    private const byte PendingBorderTileHighTransfer = 4;
    private const byte PendingBorderMapTransfer = 5;

    private readonly byte[] _command = new byte[MaxCommandSizeBytes];
    private readonly SgbBorderRenderer _renderer = new();
    private int _commandWriteBitIndex;
    private SgbPacketPhase _packetPhase;
    private int _playerCount = 1;
    private int _currentPlayer;
    private byte _maskMode;
    private byte _pendingVramTransfer;
    private byte _pendingVramTransferFrameDelay;

    public bool HasPendingVramTransfer => _pendingVramTransfer != NoPendingVramTransfer;

    internal SgbControllerState CaptureState() =>
        new(
            (byte[])_command.Clone(),
            _renderer.CaptureState(),
            _commandWriteBitIndex,
            _packetPhase,
            _playerCount,
            _currentPlayer,
            _maskMode,
            _pendingVramTransfer,
            _pendingVramTransferFrameDelay
        );

    internal void ValidateState(SgbControllerState state)
    {
        if (state.Command is null || state.Command.Length != _command.Length)
        {
            throw new ArgumentException("SGB state has an invalid buffer shape.", nameof(state));
        }

        _renderer.ValidateState(state.Renderer);

        var invalidVramTransfer = state.PendingVramTransfer switch
        {
            > PendingBorderMapTransfer => true,
            NoPendingVramTransfer => state.PendingVramTransferFrameDelay != 0,
            _ => state.PendingVramTransferFrameDelay is < 1 or > VramTransferFrameDelay,
        };
        var validPacketIndex = state.CommandWriteBitIndex is >= 0 and <= MaxCommandSizeBytes * 8;
        var validPhase = state.PacketPhase switch
        {
            SgbPacketPhase.AwaitingPacketStart => state.CommandWriteBitIndex % (PacketSizeBytes * 8)
                == 0
                && state.CommandWriteBitIndex != MaxCommandSizeBytes * 8,
            SgbPacketPhase.AwaitingPulse => state.CommandWriteBitIndex != MaxCommandSizeBytes * 8,
            SgbPacketPhase.AwaitingBit => true,
            SgbPacketPhase.AwaitingStop => state.CommandWriteBitIndex != 0
                && state.CommandWriteBitIndex % (PacketSizeBytes * 8) == 0,
            SgbPacketPhase.AwaitingStopBit => state.CommandWriteBitIndex != 0
                && state.CommandWriteBitIndex % (PacketSizeBytes * 8) == 0,
            _ => false,
        };

        if (
            !validPacketIndex
            || !validPhase
            || state.PlayerCount is not (1 or 2 or 4)
            || state.CurrentPlayer < 0
            || state.CurrentPlayer >= state.PlayerCount
            || state.MaskMode > 3
            || invalidVramTransfer
        )
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal void RestoreState(SgbControllerState state)
    {
        ValidateState(state);

        state.Command.CopyTo(_command, 0);
        _renderer.RestoreState(state.Renderer);
        _commandWriteBitIndex = state.CommandWriteBitIndex;
        _packetPhase = state.PacketPhase;
        _playerCount = state.PlayerCount;
        _currentPlayer = state.CurrentPlayer;
        _maskMode = state.MaskMode;
        _pendingVramTransfer = state.PendingVramTransfer;
        _pendingVramTransferFrameDelay = state.PendingVramTransferFrameDelay;
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

        switch (selectedGroups >> 4)
        {
            case 0b11:
                if (_packetPhase is SgbPacketPhase.AwaitingPulse)
                {
                    _packetPhase = SgbPacketPhase.AwaitingBit;
                }
                else if (_packetPhase is SgbPacketPhase.AwaitingStop)
                {
                    _packetPhase = SgbPacketPhase.AwaitingStopBit;
                }

                return;
            case 0b10:
                ReceiveBit(value: 0, GetCommandInfo());
                return;
            case 0b01:
                ReceiveBit(value: 1, GetCommandInfo());
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
        return frame.PixelFormat is LcdPixelFormat.DmgShadeIndex8
            ? _renderer.ApplyPalettes(frame, _maskMode)
            : frame;
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

        _renderer.ApplyPendingVramTransfer(transferData, _pendingVramTransfer);
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

    private (int SizeBits, bool IsSupported, bool HasValidPacketCount) GetCommandInfo()
    {
        var isSupported = IsSupportedCommand(_command[0] >> 3);
        var packetCount = _command[0] & 0x07;

        return (packetCount * PacketSizeBytes * 8, isSupported, packetCount != 0);
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
        if (
            (_commandWriteBitIndex & ((PacketSizeBytes * 8) - 1)) == 0
            && _commandWriteBitIndex != 0
            && _packetPhase is not SgbPacketPhase.AwaitingStop
        )
        {
            _packetPhase = SgbPacketPhase.AwaitingPulse;
            return;
        }

        ClearCommand();
        _packetPhase = SgbPacketPhase.AwaitingPulse;
    }

    private void ReceiveBit(
        byte value,
        (int SizeBits, bool IsSupported, bool HasValidPacketCount) commandInfo
    )
    {
        if (_packetPhase is not SgbPacketPhase.AwaitingBit and not SgbPacketPhase.AwaitingStopBit)
        {
            return;
        }

        if (_packetPhase is SgbPacketPhase.AwaitingStopBit)
        {
            if (
                value == 0
                && (
                    !commandInfo.HasValidPacketCount
                    || _commandWriteBitIndex == commandInfo.SizeBits
                )
            )
            {
                if (commandInfo.HasValidPacketCount)
                {
                    ExecuteCommand(commandInfo.IsSupported);
                }

                ClearCommand();
            }

            _packetPhase = SgbPacketPhase.AwaitingPacketStart;
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
        _packetPhase =
            (_commandWriteBitIndex & ((PacketSizeBytes * 8) - 1)) == 0
                ? SgbPacketPhase.AwaitingStop
                : SgbPacketPhase.AwaitingPulse;
    }

    private void ExecuteCommand(bool isSupported)
    {
        if (!isSupported || (_command[0] & 0x07) == 0)
        {
            return;
        }

        switch (_command[0] >> 3)
        {
            case Pal01Command:
                _renderer.SetPalettes(_command, firstPalette: 0, secondPalette: 1);
                return;
            case Pal23Command:
                _renderer.SetPalettes(_command, firstPalette: 2, secondPalette: 3);
                return;
            case Pal03Command:
                _renderer.SetPalettes(_command, firstPalette: 0, secondPalette: 3);
                return;
            case Pal12Command:
                _renderer.SetPalettes(_command, firstPalette: 1, secondPalette: 2);
                return;
            case AttrBlkCommand:
                _renderer.SetBlockAttributes(_command);
                return;
            case AttrLinCommand:
                _renderer.SetLineAttributes(_command);
                return;
            case AttrDivCommand:
                _renderer.SetDivisionAttributes(_command);
                return;
            case AttrChrCommand:
                _renderer.SetCharacterAttributes(_command);
                return;
            case PalSetCommand:
                if (_renderer.SetSystemPalettes(_command))
                {
                    _maskMode = 0;
                }

                return;
            case PalTrnCommand:
                RequestVramTransfer(PendingPaletteTransfer);
                return;
            case DataSndCommand:
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
                if (_renderer.SetAttributeFile(_command[1]))
                {
                    _maskMode = 0;
                }

                return;
            case MaskEnCommand:
                _maskMode = (byte)(_command[1] & 0x03);
                return;
        }
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

    internal void ResetPacketReceiver()
    {
        ClearCommand();
        _packetPhase = SgbPacketPhase.AwaitingPacketStart;
    }

    private void ClearCommand()
    {
        Array.Clear(_command);
        _commandWriteBitIndex = 0;
    }
}

internal enum SgbPacketPhase : byte
{
    AwaitingPacketStart = 0,
    AwaitingPulse = 1,
    AwaitingBit = 2,
    AwaitingStop = 3,
    AwaitingStopBit = 4,
}

internal readonly record struct SgbControllerState(
    byte[] Command,
    SgbBorderRendererState Renderer,
    int CommandWriteBitIndex,
    SgbPacketPhase PacketPhase,
    int PlayerCount,
    int CurrentPlayer,
    byte MaskMode,
    byte PendingVramTransfer,
    byte PendingVramTransferFrameDelay
);
