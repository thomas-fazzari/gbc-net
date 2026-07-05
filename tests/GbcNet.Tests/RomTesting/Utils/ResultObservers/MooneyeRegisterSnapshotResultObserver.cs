// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using GbcNet.Core;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class MooneyeRegisterSnapshotResultObserver : IRomResultObserver
{
    private const string Source = "Mooneye register snapshot";
    private const byte LoadBFromBOpcode = 0x40;
    private const byte FailureByte = 0x42;
    private const ushort DiagnosticHramStart = AddressMap.HighRamStart;
    private const int DiagnosticHramLength = 0x11;

    private static readonly byte[] _passReport = [0x03, 0x05, 0x08, 0x0D, 0x15, 0x22];
    private readonly GameBoy _gameBoy;

    public MooneyeRegisterSnapshotResultObserver(GameBoy gameBoy)
    {
        _gameBoy = gameBoy;
        gameBoy.Cpu.InstructionExecuted += OnInstructionExecuted;
    }

    public RomTestObservation Snapshot { get; private set; } = new(Source);

    public RomTestObservation? Observe() => Snapshot.Status is null ? null : Snapshot;

    private void OnInstructionExecuted(object? sender, CpuInstructionExecutedEventArgs e)
    {
        if (e.Opcode is not LoadBFromBOpcode)
        {
            return;
        }

        Span<byte> report =
        [
            e.Registers.B,
            e.Registers.C,
            e.Registers.D,
            e.Registers.E,
            e.Registers.H,
            e.Registers.L,
        ];
        var status = GetStatus(report);
        if (status is { } resultStatus)
        {
            Snapshot = new RomTestObservation(
                Source,
                resultStatus,
                FormatReport(report, resultStatus)
            );
        }
    }

    private static RomTestStatus? GetStatus(ReadOnlySpan<byte> report)
    {
        if (report.SequenceEqual(_passReport))
        {
            return RomTestStatus.Passed;
        }

        foreach (var value in report)
        {
            if (value is not FailureByte)
            {
                return null;
            }
        }

        return RomTestStatus.Failed;
    }

    private string FormatReport(ReadOnlySpan<byte> report, RomTestStatus status)
    {
        var output = FormatBytes(report);
        if (status is not RomTestStatus.Failed)
        {
            return output;
        }

        return new StringBuilder(output)
            .AppendLine()
            .Append("HRAM FF80-FF90: ")
            .Append(FormatDiagnosticHram())
            .ToString();
    }

    private string FormatDiagnosticHram()
    {
        Span<byte> bytes = stackalloc byte[DiagnosticHramLength];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = _gameBoy.Bus.ReadByte((ushort)(DiagnosticHramStart + index));
        }

        return FormatBytes(bytes);
    }

    private static string FormatBytes(ReadOnlySpan<byte> bytes)
    {
        var output = new StringBuilder();
        for (var index = 0; index < bytes.Length; index++)
        {
            if (index > 0)
            {
                output.Append(' ');
            }

            output.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return output.ToString();
    }
}
