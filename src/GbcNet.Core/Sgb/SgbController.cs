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
    private const byte MltReqCommand = 0x11;

    private readonly byte[] _command = new byte[MaxCommandSizeBytes];
    private int _commandWriteBitIndex;
    private bool _readyForPulse;
    private bool _readyForWrite;
    private bool _readyForStop;

    public int PlayerCount { get; private set; } = 1;

    public int CurrentPlayer { get; private set; }

    public void Write(byte value, byte previousSelectedGroups)
    {
        var selectedGroups = (byte)(value & SelectBitsMask);
        if (
            PlayerCount > 1
            && (previousSelectedGroups & P15Bit) == 0
            && (selectedGroups & P15Bit) != 0
        )
        {
            CurrentPlayer = (CurrentPlayer + 1) & (PlayerCount - 1);
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
        return selectedGroups == SelectBitsMask && PlayerCount > 1
            ? (byte)(0x0F - CurrentPlayer)
            : lowNibble;
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

        if (_command[0] >> 3 == MltReqCommand)
        {
            SetPlayerCount(_command[1] & 0x03);
        }
    }

    private void SetPlayerCount(int mode)
    {
        PlayerCount = mode switch
        {
            1 => 2,
            3 => 4,
            _ => 1,
        };
        CurrentPlayer &= PlayerCount - 1;
    }

    private void ClearCommand()
    {
        Array.Clear(_command);
        _commandWriteBitIndex = 0;
    }
}
