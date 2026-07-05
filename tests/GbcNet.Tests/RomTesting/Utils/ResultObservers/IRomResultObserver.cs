// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal interface IRomResultObserver
{
    RomTestObservation? Observe();

    RomTestObservation Snapshot { get; }
}
