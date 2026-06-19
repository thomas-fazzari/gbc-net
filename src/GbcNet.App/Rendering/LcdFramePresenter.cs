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
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _pendingFrame, frame);

        if (Interlocked.Exchange(ref _isRenderQueued, 1) == 0)
        {
            screenImage.Dispatcher.Post(RenderPendingFrame);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _pendingFrame, null);
        screenImage.Source = null;
        _renderer.Dispose();
    }

    private void RenderPendingFrame()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            Interlocked.Exchange(ref _pendingFrame, null);
            Volatile.Write(ref _isRenderQueued, 0);
            return;
        }

        var frame = Interlocked.Exchange(location1: ref _pendingFrame, value: null);
        Volatile.Write(location: ref _isRenderQueued, value: 0);

        if (frame is not null)
        {
            screenImage.Source = _renderer.Render(frame);
        }

        // Drop intermediate fast-forward frames to avoid dispatcher backlog
        if (
            Volatile.Read(ref _isDisposed) == 0
            && Interlocked.CompareExchange(
                location1: ref _pendingFrame,
                value: null,
                comparand: null
            )
                is not null
            && Interlocked.Exchange(location1: ref _isRenderQueued, value: 1) == 0
        )
        {
            screenImage.Dispatcher.Post(RenderPendingFrame);
        }
    }
}
