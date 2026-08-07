// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Runtime.InteropServices;
using GbcNet.Core;

namespace GbcNet.Tests.Unit.RomTesting.Utils.ResultObservers;

internal sealed class MooneyeSerialResultObserver : IRomResultObserver
{
    private const string Source = "Mooneye serial";

    private readonly List<byte> _output = [];

    public MooneyeSerialResultObserver(GameBoy gameBoy)
    {
        gameBoy.SerialByteTransferred += _output.Add;
    }

    public RomTestObservation Snapshot => new(Source, Output: FormatOutput());

    public RomTestObservation? Observe()
    {
        if (ContainsReport(MooneyeReport.PassReport))
        {
            return new RomTestObservation(Source, RomTestStatus.Passed, FormatOutput());
        }

        return ContainsReport(MooneyeReport.FailReport)
            ? new RomTestObservation(Source, RomTestStatus.Failed, FormatOutput())
            : null;
    }

    private bool ContainsReport(ReadOnlySpan<byte> report) =>
        CollectionsMarshal.AsSpan(_output).IndexOf(report) >= 0;

    private string FormatOutput() =>
        string.Join(
            ' ',
            _output.Select(value => value.ToString("X2", CultureInfo.InvariantCulture))
        );
}
