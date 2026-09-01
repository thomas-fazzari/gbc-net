// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils.ResultObservers;

internal interface IRomResultObserver
{
    RomTestObservation? Observe();

    RomTestObservation Snapshot { get; }
}
