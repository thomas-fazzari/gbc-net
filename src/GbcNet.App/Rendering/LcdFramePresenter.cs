// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using GbcNet.Core.Ppu;

namespace GbcNet.App.Rendering;

internal sealed class LcdFramePresenter(Image screenImage) : IDisposable
{
    private readonly LcdFrameBitmapRenderer _renderer = new();
    private LcdFrame? _pendingFrame;
    private int _isRenderQueued;
    private int _isDisposed;

    internal void Enqueue(LcdFrame frame)
    {
        if (Volatile.Read(location: ref _isDisposed) != 0)
        {
            frame.Dispose();
            return;
        }

        Interlocked.Exchange(location1: ref _pendingFrame, value: frame)?.Dispose();

        if (Volatile.Read(location: ref _isDisposed) != 0)
        {
            if (
                ReferenceEquals(
                    Interlocked.CompareExchange(
                        location1: ref _pendingFrame,
                        value: null,
                        comparand: frame
                    ),
                    frame
                )
            )
            {
                frame.Dispose();
            }

            return;
        }

        if (Interlocked.Exchange(location1: ref _isRenderQueued, value: 1) == 0)
        {
            QueueRender();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(location1: ref _isDisposed, value: 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(location1: ref _pendingFrame, value: null)?.Dispose();
        screenImage.Source = null;
        _renderer.Dispose();
    }

    private void RenderPendingFrame()
    {
        if (Volatile.Read(location: ref _isDisposed) != 0)
        {
            Interlocked.Exchange(location1: ref _pendingFrame, value: null)?.Dispose();
            Volatile.Write(location: ref _isRenderQueued, value: 0);
            return;
        }

        var frame = Interlocked.Exchange(location1: ref _pendingFrame, value: null);
        Volatile.Write(location: ref _isRenderQueued, value: 0);

        if (frame is not null)
        {
            using (frame)
            {
                screenImage.Width = frame.Width;
                screenImage.Height = frame.Height;
                screenImage.Source = _renderer.Render(frame);
            }
        }

        // Drop intermediate fast-forward frames to avoid dispatcher backlog
        if (
            Volatile.Read(location: ref _isDisposed) == 0
            && Interlocked.CompareExchange(
                location1: ref _pendingFrame,
                value: null,
                comparand: null
            )
                is not null
            && Interlocked.Exchange(location1: ref _isRenderQueued, value: 1) == 0
        )
        {
            QueueRender();
        }
    }

    private void QueueRender()
    {
        try
        {
            screenImage.Dispatcher.Post(action: RenderPendingFrame);
        }
        catch
        {
            Volatile.Write(location: ref _isRenderQueued, value: 0);
            Interlocked.Exchange(location1: ref _pendingFrame, value: null)?.Dispose();
            throw;
        }
    }
}
