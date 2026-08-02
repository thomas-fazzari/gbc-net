// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// Pixel source currently fetched by the background/window FIFO fetcher.
/// </summary>
internal enum PixelFetcherSource
{
    Background = 0,
    Window = 1,
}
