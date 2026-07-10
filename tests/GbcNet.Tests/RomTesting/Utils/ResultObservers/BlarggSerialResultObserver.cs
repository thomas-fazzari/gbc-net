// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using GbcNet.Core;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class BlarggSerialResultObserver : IRomResultObserver
{
    private const string Source = "Serial";
    private const string PassedMarker = "Passed";
    private const string FailedMarker = "Failed";

    private readonly StringBuilder _output = new();

    public BlarggSerialResultObserver(GameBoy gameBoy)
    {
        gameBoy.SerialByteTransferred += transferredByte => _output.Append((char)transferredByte);
    }

    public RomTestObservation Snapshot => new(Source, Output: _output.ToString());

    public RomTestObservation? Observe()
    {
        var output = _output.ToString();
        if (output.Contains(PassedMarker, StringComparison.Ordinal))
        {
            return new RomTestObservation(Source, RomTestStatus.Passed, output);
        }

        return output.Contains(FailedMarker, StringComparison.Ordinal)
            ? new RomTestObservation(Source, RomTestStatus.Failed, output)
            : null;
    }
}
