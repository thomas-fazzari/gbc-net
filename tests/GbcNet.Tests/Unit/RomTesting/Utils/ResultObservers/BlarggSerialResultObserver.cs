// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using GbcNet.Core;

namespace GbcNet.Tests.Unit.RomTesting.Utils.ResultObservers;

/// <summary>
/// Detects the case-sensitive Blargg Passed and Failed markers in serial text.
/// </summary>
internal sealed class BlarggSerialResultObserver : IRomResultObserver
{
    private const string Source = "Serial";
    private const string PassedMarker = "Passed";
    private const string FailedMarker = "Failed";

    private readonly StringBuilder _output = new();

    /// <summary>
    /// Starts appending transferred serial bytes from <paramref name="gameBoy"/> as characters.
    /// </summary>
    public BlarggSerialResultObserver(GameBoy gameBoy)
    {
        gameBoy.SerialByteTransferred += transferredByte => _output.Append((char)transferredByte);
    }

    /// <inheritdoc />
    public RomTestObservation Snapshot => new(Source, Output: _output.ToString());

    /// <inheritdoc />
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
