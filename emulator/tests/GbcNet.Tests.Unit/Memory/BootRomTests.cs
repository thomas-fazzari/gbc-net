// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Memory;

public sealed class BootRomTests
{
    [Fact]
    public void CaptureRestore_ReversesBootRomUnmapping()
    {
        var bytes = new byte[BootRomOptions.DmgBootRomSize];
        bytes[0] = 0x31;
        var bootRom = BootRom
            .Create(HardwareModel.Dmg, new BootRomOptions { DmgBootRom = bytes })
            .Should()
            .BeOfType<BootRom>()
            .Subject;

        var state = bootRom.CaptureState();
        bootRom.WriteDisableRegister(0x01);

        bootRom.IsMapped.Should().BeFalse();
        bootRom.TryRead(0x0000, out _).Should().BeFalse();

        bootRom.RestoreState(state);

        bootRom.IsMapped.Should().BeTrue();
        bootRom.TryRead(0x0000, out var value).Should().BeTrue();
        value.Should().Be(0x31);
    }
}
