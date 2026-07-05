// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using GbcNet.Core;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class BlarggExternalRamResultObserver : IRomResultObserver
{
    private const string Source = "Blargg external RAM";
    private const byte RunningStatus = 0x80;
    private const byte PassedStatus = 0x00;
    private const byte Signature0 = 0xDE;
    private const byte Signature1 = 0xB0;
    private const byte Signature2 = 0x61;
    private const ushort StatusAddress = AddressMap.ExternalRamStart;
    private const ushort SignatureAddress = StatusAddress + 1;
    private const ushort TextAddress = StatusAddress + 4;
    private const int TextMaxLength =
        AddressMap.ExternalRamWindowSize - (TextAddress - AddressMap.ExternalRamStart);

    private readonly byte[] _externalRamWrites = new byte[AddressMap.ExternalRamWindowSize];

    public BlarggExternalRamResultObserver(GameBoy gameBoy)
    {
        gameBoy.Bus.CpuMemoryWritten += OnCpuMemoryWritten;
    }

    public RomTestObservation Snapshot { get; private set; } = new(Source);

    public RomTestObservation? Observe()
    {
        if (!HasSignature())
        {
            Snapshot = new RomTestObservation(Source);
            return null;
        }

        var statusCode = ReadObservedByte(StatusAddress);
        var output = ReadOutput();
        RomTestStatus? status = statusCode switch
        {
            RunningStatus => null,
            PassedStatus => RomTestStatus.Passed,
            _ => RomTestStatus.Failed,
        };
        Snapshot = new RomTestObservation(Source, status, output, statusCode);

        return status is null ? null : Snapshot;
    }

    private void OnCpuMemoryWritten(object? sender, CpuMemoryWrittenEventArgs e)
    {
        if (e.Address is >= AddressMap.ExternalRamStart and <= AddressMap.ExternalRamEnd)
        {
            _externalRamWrites[e.Address - AddressMap.ExternalRamStart] = e.Value;
        }
    }

    private bool HasSignature() =>
        ReadObservedByte(SignatureAddress) is Signature0
        && ReadObservedByte(SignatureAddress + 1) is Signature1
        && ReadObservedByte(SignatureAddress + 2) is Signature2;

    private string ReadOutput()
    {
        var output = new StringBuilder();
        for (var offset = 0; offset < TextMaxLength; offset++)
        {
            var value = ReadObservedByte(TextAddress + offset);
            if (value == 0)
            {
                break;
            }

            output.Append((char)value);
        }

        return output.ToString();
    }

    private byte ReadObservedByte(int address) =>
        _externalRamWrites[address - AddressMap.ExternalRamStart];
}
