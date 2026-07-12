// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Memory;

/// <summary>
/// Stores a plain contiguous address window with no hardware side effects or read/write masks.
/// </summary>
internal sealed class MappedMemory(ushort startAddress, ushort endAddress)
{
    private readonly byte[] _bytes = new byte[endAddress - startAddress + 1];

    public byte Read(ushort address) => _bytes[address - startAddress];

    public void Write(ushort address, byte value)
    {
        _bytes[address - startAddress] = value;
    }

    internal MappedMemoryState CaptureState() => new(_bytes.ToArray());

    internal void ValidateState(MappedMemoryState state)
    {
        var bytes = state.Bytes;
        if (bytes is null || bytes.Length != _bytes.Length)
        {
            throw new ArgumentException(
                "State bytes must match the mapped memory length.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(MappedMemoryState state)
    {
        ValidateState(state);
        state.Bytes.CopyTo(_bytes, 0);
    }
}

internal readonly record struct MappedMemoryState(byte[] Bytes);
