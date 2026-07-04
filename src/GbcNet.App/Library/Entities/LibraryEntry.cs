// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;

namespace GbcNet.App.Library.Entities;

/// <summary>
/// A ROM, identified by its hash.
/// </summary>
internal sealed record LibraryEntry(
    string RomHash,
    string LastKnownPath,
    string FileName,
    string? CartridgeTitle,
    CartridgeHardwareKind HardwareKind,
    DateTimeOffset AddedAt,
    DateTimeOffset LastOpenedAt,
    int LaunchCount,
    // TODO: Populate when implementing cover art feature.
    string? CoverPath
);
