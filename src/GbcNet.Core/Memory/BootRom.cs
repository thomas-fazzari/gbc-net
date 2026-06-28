using System.Globalization;
using GbcNet.Core.Hardware;

namespace GbcNet.Core.Memory;

/// <summary>
/// CPU-visible boot ROM overlay mapped before the cartridge ROM window is handed over.
/// </summary>
internal sealed class BootRom
{
    private const ushort LowerEnd = 0x00FF;
    private const ushort CgbUpperStart = 0x0200;
    private const ushort CgbUpperEnd = 0x08FF;
    private const int CgbUpperFileOffset = 0x0100;

    private readonly byte[] _bytes;
    private readonly HardwareModel _hardwareModel;
    private bool _mapped = true;

    private BootRom(HardwareModel hardwareModel, ReadOnlyMemory<byte> bytes)
    {
        _hardwareModel = hardwareModel;
        _bytes = bytes.ToArray();
    }

    internal static BootRom? Create(HardwareModel hardwareModel, BootRomOptions options)
    {
        var bytes = hardwareModel switch
        {
            HardwareModel.Dmg => options.DmgBootRom,
            HardwareModel.Cgb => options.CgbBootRom,
            HardwareModel.Sgb => options.SgbBootRom,
            _ => throw new ArgumentOutOfRangeException(
                nameof(hardwareModel),
                hardwareModel,
                "Unsupported hardware model."
            ),
        };

        if (bytes.IsEmpty)
        {
            return null;
        }

        var expectedLength = hardwareModel switch
        {
            HardwareModel.Dmg => BootRomOptions.DmgBootRomSize,
            HardwareModel.Cgb => BootRomOptions.CgbBootRomSize,
            HardwareModel.Sgb => BootRomOptions.SgbBootRomSize,
            _ => throw new ArgumentOutOfRangeException(
                nameof(hardwareModel),
                hardwareModel,
                "Unsupported hardware model."
            ),
        };

        if (bytes.Length != expectedLength)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} boot ROM must be {1} bytes, but was {2} bytes.",
                    hardwareModel,
                    expectedLength,
                    bytes.Length
                ),
                nameof(options)
            );
        }

        return new BootRom(hardwareModel, bytes);
    }

    internal bool TryRead(ushort address, out byte value)
    {
        if (!_mapped)
        {
            value = 0;
            return false;
        }

        if (address <= LowerEnd)
        {
            value = _bytes[address];
            return true;
        }

        if (_hardwareModel is HardwareModel.Cgb && address is >= CgbUpperStart and <= CgbUpperEnd)
        {
            value = _bytes[CgbUpperFileOffset + address - CgbUpperStart];
            return true;
        }

        value = 0;
        return false;
    }

    internal void WriteDisableRegister(byte value)
    {
        if (value != 0)
        {
            _mapped = false;
        }
    }
}
