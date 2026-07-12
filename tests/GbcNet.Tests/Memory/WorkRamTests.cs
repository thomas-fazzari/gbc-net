// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Tests.Memory;

public sealed class WorkRamTests
{
    [Fact]
    public void ReadWrite_MapsFixedBankAndSwitchableBankIndependently()
    {
        var workRam = new WorkRam(bankCount: 8);

        workRam.Write(0xC000, 0x11);
        workRam.Write(0xD000, 0x22);
        workRam.SelectSwitchableBank(2);
        workRam.Write(0xD000, 0x33);

        Assert.Equal(0x11, workRam.Read(0xC000));
        Assert.Equal(0x33, workRam.Read(0xD000));

        workRam.SelectSwitchableBank(1);

        Assert.Equal(0x22, workRam.Read(0xD000));
    }

    [Fact]
    public void ReadWrite_MirrorsEchoRamThroughSelectedBanks()
    {
        var workRam = new WorkRam(bankCount: 8);

        workRam.Write(0xC000, 0x44);
        workRam.SelectSwitchableBank(3);
        workRam.Write(0xF000, 0x55);

        Assert.Equal(0x44, workRam.Read(0xE000));
        Assert.Equal(0x55, workRam.Read(0xD000));
    }

    [Fact]
    public void SelectSwitchableBank_ZeroMapsToBankOne()
    {
        var workRam = new WorkRam(bankCount: 8);

        workRam.Write(0xD000, 0x66);
        workRam.SelectSwitchableBank(2);
        workRam.Write(0xD000, 0x77);
        workRam.SelectSwitchableBank(0);

        Assert.Equal(0x66, workRam.Read(0xD000));
    }

    [Fact]
    public void ReadWriteBankRegister_ZeroMapsToBankOneButReadBackPreservesZero()
    {
        var workRam = new WorkRam(bankCount: 8, isBankRegisterEnabled: true);

        workRam.Write(AddressMap.WorkRamSwitchableBankStart, 0x11);
        workRam.WriteBankRegister(2);
        workRam.Write(AddressMap.WorkRamSwitchableBankStart, 0x22);
        workRam.WriteBankRegister(0);

        Assert.Equal(0xF8, workRam.ReadBankRegister());
        Assert.Equal(0x11, workRam.Read(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteBankRegister_SevenMapsBankSevenAndReadBackSetsUpperBits()
    {
        var workRam = new WorkRam(bankCount: 8, isBankRegisterEnabled: true);

        workRam.WriteBankRegister(7);
        workRam.Write(AddressMap.WorkRamSwitchableBankStart, 0x77);
        workRam.WriteBankRegister(1);

        Assert.Equal(0xF9, workRam.ReadBankRegister());
        Assert.Equal(0x00, workRam.Read(AddressMap.WorkRamSwitchableBankStart));

        workRam.WriteBankRegister(7);

        Assert.Equal(0xFF, workRam.ReadBankRegister());
        Assert.Equal(0x77, workRam.Read(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteBankRegister_IgnoresRegisterWhenDisabled()
    {
        var workRam = new WorkRam(bankCount: 8);

        workRam.Write(AddressMap.WorkRamSwitchableBankStart, 0x12);
        workRam.WriteBankRegister(7);
        workRam.Write(AddressMap.WorkRamSwitchableBankStart, 0x34);

        Assert.Equal(0xFF, workRam.ReadBankRegister());
        Assert.Equal(0x34, workRam.Read(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void SelectSwitchableBank_UnsupportedBankMapsToBankOne()
    {
        var workRam = new WorkRam(bankCount: 2);

        workRam.Write(0xD000, 0x88);
        workRam.SelectSwitchableBank(7);

        Assert.Equal(0x88, workRam.Read(0xD000));
    }

    [Fact]
    public void CaptureRestoreState_RestoresBanksAndRawBankRegisterIndependently()
    {
        var source = new WorkRam(bankCount: 8, isBankRegisterEnabled: true);
        source.Write(AddressMap.WorkRamStart, 0x10);
        source.WriteBankRegister(1);
        source.Write(AddressMap.WorkRamSwitchableBankStart, 0x11);
        source.WriteBankRegister(7);
        source.Write(AddressMap.WorkRamSwitchableBankStart, 0x77);
        source.WriteBankRegister(0);

        var state = source.CaptureState();

        source.Write(AddressMap.WorkRamStart, 0x20);
        source.Write(AddressMap.WorkRamSwitchableBankStart, 0x22);
        source.WriteBankRegister(7);
        source.Write(AddressMap.WorkRamSwitchableBankStart, 0x88);

        var restored = new WorkRam(bankCount: 8, isBankRegisterEnabled: true);
        restored.WriteBankRegister(7);
        restored.RestoreState(state);

        state.Banks[0] = 0x30;
        state.Banks[0x1000] = 0x33;
        state.Banks[7 * 0x1000] = 0x99;

        Assert.Equal(0x10, restored.Read(AddressMap.WorkRamStart));
        Assert.Equal(0xF8, restored.ReadBankRegister());
        Assert.Equal(0x11, restored.Read(AddressMap.WorkRamSwitchableBankStart));

        restored.WriteBankRegister(7);

        Assert.Equal(0x77, restored.Read(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void RestoreState_RejectsWrongBankLengthWithoutMutating()
    {
        var workRam = new WorkRam(bankCount: 8, isBankRegisterEnabled: true);
        workRam.Write(AddressMap.WorkRamStart, 0xAA);
        workRam.WriteBankRegister(7);
        workRam.Write(AddressMap.WorkRamSwitchableBankStart, 0xBB);

        Assert.Throws<ArgumentException>(() =>
            workRam.RestoreState(new WorkRamState(new byte[0x1000], 0))
        );

        Assert.Equal(0xAA, workRam.Read(AddressMap.WorkRamStart));
        Assert.Equal(0xFF, workRam.ReadBankRegister());
        Assert.Equal(0xBB, workRam.Read(AddressMap.WorkRamSwitchableBankStart));
    }
}
