// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Serialization;

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Mutable state for one PPU engine implementation.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DmgPpuEngineState), "dmg")]
[JsonDerivedType(typeof(CgbDmgCompatibilityPpuEngineState), "cgb-dmg")]
[JsonDerivedType(typeof(CgbPpuEngineState), "cgb")]
internal interface IPpuEngineState;
