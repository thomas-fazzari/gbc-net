// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Memory;

public sealed class CgbMiscRegistersTests
{
    [Fact]
    public void CaptureRestore_ContinuesAllRawRegistersWithMaskedFf75Readback()
    {
        var registers = new CgbMiscRegisters(
            isCgbHardwareRegisterEnabled: true,
            isFf74Enabled: true
        );
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf72, 0x12);
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf73, 0x34);
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf74, 0x56);
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf75, 0x1F);
        var state = registers.CaptureState();
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf72, 0xAA);
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf73, 0xBB);
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf74, 0xCC);
        registers.WriteRegister(AddressMap.CgbUndocumentedRegisterFf75, 0x00);

        registers.RestoreState(state);

        Assert.Equal(0x12, registers.ReadRegister(AddressMap.CgbUndocumentedRegisterFf72));
        Assert.Equal(0x34, registers.ReadRegister(AddressMap.CgbUndocumentedRegisterFf73));
        Assert.Equal(0x56, registers.ReadRegister(AddressMap.CgbUndocumentedRegisterFf74));
        Assert.Equal(0x9F, registers.ReadRegister(AddressMap.CgbUndocumentedRegisterFf75));
    }
}
