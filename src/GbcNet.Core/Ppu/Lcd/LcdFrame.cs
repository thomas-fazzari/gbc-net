// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Immutable LCD frame snapshot emitted after a visible frame is complete.
/// </summary>
public sealed class LcdFrame : IDisposable
{
    private PixelBufferOwner? _pixels;

    internal LcdFrame(int width, int height, LcdPixelFormat pixelFormat, ReadOnlySpan<byte> pixels)
        : this(width, height, pixelFormat, new OwnedPixelBuffer(pixels.ToArray())) { }

    /// <summary>
    /// Creates a frame that owns an already completed pixel buffer.
    /// </summary>
    internal static LcdFrame FromOwnedPixels(
        int width,
        int height,
        LcdPixelFormat pixelFormat,
        byte[] pixels
    )
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return new LcdFrame(width, height, pixelFormat, new OwnedPixelBuffer(pixels));
    }

    internal static LcdFrame FromPooledPixels(
        int width,
        int height,
        LcdPixelFormat pixelFormat,
        ArrayPool<byte> pool,
        byte[] pixels,
        int length
    )
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length > pixels.Length)
        {
            throw new ArgumentException(
                "Pixel length cannot exceed the buffer length.",
                nameof(length)
            );
        }

        return new LcdFrame(
            width,
            height,
            pixelFormat,
            new PooledPixelBuffer(pool, pixels, length)
        );
    }

    private LcdFrame(int width, int height, LcdPixelFormat pixelFormat, PixelBufferOwner pixels)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        _pixels = pixels;
    }

    /// <summary>
    /// Frame width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Frame height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Pixel encoding used by the frame data.
    /// </summary>
    public LcdPixelFormat PixelFormat { get; }

    /// <summary>
    /// Immutable row-major pixel data, valid until this frame is disposed.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels =>
        Volatile.Read(ref _pixels)?.Pixels ?? throw new ObjectDisposedException(nameof(LcdFrame));

    internal LcdFrame Retain()
    {
        var pixels =
            Volatile.Read(ref _pixels) ?? throw new ObjectDisposedException(nameof(LcdFrame));

        pixels.Retain();

        return new LcdFrame(Width, Height, PixelFormat, pixels);
    }

    /// <summary>
    /// Releases pixel storage owned by this frame.
    /// </summary>
    public void Dispose() => Interlocked.Exchange(ref _pixels, null)?.Release();

    private abstract class PixelBufferOwner(ReadOnlyMemory<byte> pixels)
    {
        private int _referenceCount = 1;

        internal ReadOnlyMemory<byte> Pixels { get; } = pixels;

        internal void Retain()
        {
            var referenceCount = Volatile.Read(ref _referenceCount);
            while (referenceCount > 0)
            {
                var observed = Interlocked.CompareExchange(
                    ref _referenceCount,
                    referenceCount + 1,
                    referenceCount
                );

                if (observed == referenceCount)
                {
                    return;
                }

                referenceCount = observed;
            }

            throw new ObjectDisposedException(nameof(LcdFrame));
        }

        internal void Release()
        {
            if (Interlocked.Decrement(ref _referenceCount) != 0)
            {
                return;
            }

            ReleasePixels();
        }

        protected abstract void ReleasePixels();
    }

    private sealed class OwnedPixelBuffer(byte[] pixels) : PixelBufferOwner(pixels)
    {
        protected override void ReleasePixels() { }
    }

    private sealed class PooledPixelBuffer(ArrayPool<byte> pool, byte[] pixels, int length)
        : PixelBufferOwner(pixels.AsMemory(start: 0, length))
    {
        private byte[]? _array = pixels;

        protected override void ReleasePixels()
        {
            var array = Interlocked.Exchange(ref _array, null);

            if (array is not null)
            {
                pool.Return(array);
            }
        }
    }
}
