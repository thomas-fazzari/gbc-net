// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Memory;

public sealed class MappedMemoryTests
{
    [Fact]
    public void CaptureState_ClonesMemoryBytes()
    {
        var memory = new MappedMemory(0xA000, 0xA001);
        memory.Write(0xA000, 0x12);

        var state = memory.CaptureState();
        memory.Write(0xA000, 0x34);

        state.Bytes[0].Should().Be(0x12);

        state.Bytes[0] = 0x56;
        memory.Read(0xA000).Should().Be(0x34);
    }

    [Fact]
    public void RestoreState_RestoresBytesWithoutAdoptingStateArray()
    {
        var memory = new MappedMemory(0xA000, 0xA001);
        memory.Write(0xA000, 0x12);
        memory.Write(0xA001, 0x34);
        var state = memory.CaptureState();
        memory.Write(0xA000, 0x56);
        memory.Write(0xA001, 0x78);

        memory.RestoreState(state);
        state.Bytes[0] = 0x9A;

        memory.Read(0xA000).Should().Be(0x12);
        memory.Read(0xA001).Should().Be(0x34);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void RestoreState_RejectsWrongLengthWithoutMutatingMemory(int length)
    {
        var memory = new MappedMemory(0xA000, 0xA001);
        memory.Write(0xA000, 0x12);
        memory.Write(0xA001, 0x34);

        FluentActions
            .Invoking(() => memory.RestoreState(new MappedMemoryState(new byte[length])))
            .Should()
            .ThrowExactly<ArgumentException>();

        memory.Read(0xA000).Should().Be(0x12);
        memory.Read(0xA001).Should().Be(0x34);
    }
}
