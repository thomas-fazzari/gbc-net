// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.App.Rendering;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit.App.Rendering;

public sealed class LcdFramePresenterTests
{
    [Fact]
    public void Dispose_ReleasesPendingFrame()
    {
        var frame = LcdFrame.FromOwnedPixels(1, 1, LcdPixelFormat.Rgb555Le, [0x00, 0x00]);
        var presenter = new LcdFramePresenter(new Image());

        try
        {
            presenter.Enqueue(frame);
        }
        finally
        {
            presenter.Dispose();
        }

        FluentActions
            .Invoking(() => _ = frame.Pixels)
            .Should()
            .ThrowExactly<ObjectDisposedException>();
    }
}
