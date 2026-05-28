using GbcNet.Core.Dma;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Dma;

public sealed class DmaControllerTests
{
    [Fact]
    public void StartOamTransfer_StoresSourceHighByte()
    {
        var dma = new DmaController();

        dma.StartOamTransfer(0xC0);

        Assert.Equal(0xC0, dma.ReadRegister());
    }

    [Fact]
    public void SetRegisterState_SeedsRegisterWithoutStartingTransfer()
    {
        var dma = new DmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.SetRegisterState(0xFF);
        dma.Tick(160, ReadLowByte, (address, value) => writes.Add((address, value)));

        Assert.Equal(0xFF, dma.ReadRegister());
        Assert.Empty(writes);
    }

    [Fact]
    public void Tick_SkipsCurrentTickAfterTransferStart()
    {
        var dma = new DmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));

        Assert.Empty(writes);
    }

    [Fact]
    public void Tick_CopiesOneBytePerMachineCycleAfterSkippedTick()
    {
        var dma = new DmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));

        (ushort address, byte value) = Assert.Single(writes);
        Assert.Equal(AddressMap.ObjectAttributeMemoryStart, address);
        Assert.Equal(0x00, value);
    }

    [Fact]
    public void Tick_CopiesPartialTransfer()
    {
        var dma = new DmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(3, ReadLowByte, (address, value) => writes.Add((address, value)));

        Assert.Collection(
            writes,
            write =>
            {
                Assert.Equal(0xFE00, write.Address);
                Assert.Equal(0x00, write.Value);
            },
            write =>
            {
                Assert.Equal(0xFE01, write.Address);
                Assert.Equal(0x01, write.Value);
            },
            write =>
            {
                Assert.Equal(0xFE02, write.Address);
                Assert.Equal(0x02, write.Value);
            }
        );
    }

    [Fact]
    public void Tick_CompletesTransferAfterOneHundredSixtyCopiedBytes()
    {
        var dma = new DmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(159, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));

        Assert.Equal(160, writes.Count);
        Assert.Equal((AddressMap.ObjectAttributeMemoryStart, (byte)0x00), writes[0]);
        Assert.Equal((AddressMap.ObjectAttributeMemoryEnd, (byte)0x9F), writes[^1]);
    }

    [Fact]
    public void StartOamTransfer_RestartsTransferFromOffsetZero()
    {
        var dma = new DmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));

        dma.StartOamTransfer(0xD0);
        dma.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));

        Assert.Collection(
            writes,
            write =>
            {
                Assert.Equal(AddressMap.ObjectAttributeMemoryStart, write.Address);
                Assert.Equal(0xC0, write.Value);
            },
            write =>
            {
                Assert.Equal(AddressMap.ObjectAttributeMemoryStart, write.Address);
                Assert.Equal(0xD0, write.Value);
            }
        );
    }

    private static byte ReadLowByte(ushort address) => (byte)address;

    private static byte ReadSourceHighByte(ushort address) => (byte)(address >> 8);
}
