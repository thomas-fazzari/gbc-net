using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class RomTestCartridgeFactory
{
    private const ushort ProgramStartAddress = CartridgeHeader.HeaderEndAddress + 1;

    private const byte JumpImmediate16Opcode = 0xC3;
    private const byte LoadAccumulatorImmediate8Opcode = 0x3E;
    private const byte LoadHighAddressImmediate8FromAccumulatorOpcode = 0xE0;
    private const byte LoadAccumulatorFromHighAddressImmediate8Opcode = 0xF0;
    private const byte AndAccumulatorImmediate8Opcode = 0xE6;
    private const byte JumpRelativeNotZeroImmediate8Opcode = 0x20;
    private const byte HaltOpcode = 0x76;

    private const byte SerialTransferDataOffset =
        AddressMap.SerialTransferDataRegister - AddressMap.IoRegistersStart;
    private const byte SerialTransferControlOffset =
        AddressMap.SerialTransferControlRegister - AddressMap.IoRegistersStart;
    private const byte SerialTransferStartMask = 0x80;
    private const byte SerialInternalClockMask = 0x01;
    private const byte StartInternalSerialTransfer =
        SerialTransferStartMask | SerialInternalClockMask;

    public static byte[] CreateSerialOutputRom(ReadOnlySpan<byte> serialOutput)
    {
        byte[] outputBytes = serialOutput.ToArray();
        return TestRomFactory.Create(bytes => WriteSerialOutputProgram(bytes, outputBytes));
    }

    private static void WriteSerialOutputProgram(byte[] bytes, ReadOnlySpan<byte> serialOutput)
    {
        WriteJump(bytes, AddressMap.CartridgeEntryPointStart, ProgramStartAddress);

        int address = ProgramStartAddress;
        foreach (byte value in serialOutput)
        {
            address = WriteSerialTransfer(bytes, address, value);
        }

        bytes[address] = HaltOpcode;
    }

    private static void WriteJump(byte[] bytes, int address, ushort destination)
    {
        bytes[address++] = JumpImmediate16Opcode;
        bytes[address++] = (byte)(destination & 0x00FF);
        bytes[address] = (byte)(destination >> 8);
    }

    private static int WriteSerialTransfer(byte[] bytes, int address, byte value)
    {
        bytes[address++] = LoadAccumulatorImmediate8Opcode;
        bytes[address++] = value;
        bytes[address++] = LoadHighAddressImmediate8FromAccumulatorOpcode;
        bytes[address++] = SerialTransferDataOffset;
        bytes[address++] = LoadAccumulatorImmediate8Opcode;
        bytes[address++] = StartInternalSerialTransfer;
        bytes[address++] = LoadHighAddressImmediate8FromAccumulatorOpcode;
        bytes[address++] = SerialTransferControlOffset;

        return WriteWaitForSerialTransfer(bytes, address);
    }

    private static int WriteWaitForSerialTransfer(byte[] bytes, int address)
    {
        int loopAddress = address;
        bytes[address++] = LoadAccumulatorFromHighAddressImmediate8Opcode;
        bytes[address++] = SerialTransferControlOffset;
        bytes[address++] = AndAccumulatorImmediate8Opcode;
        bytes[address++] = SerialTransferStartMask;
        bytes[address++] = JumpRelativeNotZeroImmediate8Opcode;
        bytes[address++] = unchecked((byte)(loopAddress - address));

        return address;
    }
}
