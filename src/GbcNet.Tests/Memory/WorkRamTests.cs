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
    public void SelectSwitchableBank_UnsupportedBankMapsToBankOne()
    {
        var workRam = new WorkRam(bankCount: 2);

        workRam.Write(0xD000, 0x88);
        workRam.SelectSwitchableBank(7);

        Assert.Equal(0x88, workRam.Read(0xD000));
    }
}
