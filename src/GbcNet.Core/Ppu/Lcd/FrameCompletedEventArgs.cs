// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// Provides the LCD frame completed at VBlank entry.
/// </summary>
public sealed class FrameCompletedEventArgs(LcdFrame frame) : EventArgs
{
    /// <summary>
    /// Immutable LCD frame snapshot.
    /// </summary>
    public LcdFrame Frame { get; } = frame;
}
