// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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

    internal bool IsMapped => _mapped;

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

        if (!BootRomOptions.IsValidSize(hardwareModel, bytes.Length))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} boot ROM must be {1} bytes, but was {2} bytes.",
                    hardwareModel,
                    BootRomOptions.SizeDescription(hardwareModel),
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
            // CGB dumps are commonly stored either packed (0000-00FF + 0200-08FF)
            // or mapped with the unused 0100-01FF gap still present.
            value =
                _bytes.Length == BootRomOptions.CgbBootRomMappedSize
                    ? _bytes[address]
                    : _bytes[CgbUpperFileOffset + address - CgbUpperStart];
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
