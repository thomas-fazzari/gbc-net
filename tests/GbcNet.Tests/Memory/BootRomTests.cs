// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Memory;

public sealed class BootRomTests
{
    [Fact]
    public void CaptureRestore_ReversesBootRomUnmapping()
    {
        var bytes = new byte[BootRomOptions.DmgBootRomSize];
        bytes[0] = 0x31;
        var bootRom = Assert.IsType<BootRom>(
            BootRom.Create(HardwareModel.Dmg, new BootRomOptions { DmgBootRom = bytes })
        );

        var state = bootRom.CaptureState();
        bootRom.WriteDisableRegister(0x01);

        Assert.False(bootRom.IsMapped);
        Assert.False(bootRom.TryRead(0x0000, out _));

        bootRom.RestoreState(state);

        Assert.True(bootRom.IsMapped);
        Assert.True(bootRom.TryRead(0x0000, out var value));
        Assert.Equal(0x31, value);
    }
}
