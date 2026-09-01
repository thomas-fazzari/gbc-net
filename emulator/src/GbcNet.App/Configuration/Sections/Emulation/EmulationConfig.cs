// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;

namespace GbcNet.App.Configuration.Sections.Emulation;

internal sealed class EmulationConfig
{
    public bool FastForwardEnabled { get; set; }

    public EmulationSpeed FastForwardSpeed { get; set; } = EmulationSpeed.Two;
}
