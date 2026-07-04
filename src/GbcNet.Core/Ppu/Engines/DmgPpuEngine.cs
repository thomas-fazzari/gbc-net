// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// DMG LCD engine using DMG pixel rules and DMG shade-index frame output.
/// </summary>
internal sealed class DmgPpuEngine : DmgPixelRulesPpuEngine<DmgShadePixelOutput>;
