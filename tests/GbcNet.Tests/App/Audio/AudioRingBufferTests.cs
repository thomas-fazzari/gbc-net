// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
using GbcNet.Core.Apu;

namespace GbcNet.Tests.App.AudioBuffer;

public sealed class AudioRingBufferTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(-1));
    }

    [Fact]
    public void EnqueueAndTryDequeue_PreserveSampleOrder()
    {
        var buffer = new AudioRingBuffer(capacity: 3);

        buffer.Enqueue([new ApuStereoSample(1, 2), new ApuStereoSample(3, 4)]);

        Assert.Equal(2, buffer.Count);
        Assert.True(buffer.TryDequeue(out var first));
        Assert.Equal(new ApuStereoSample(1, 2), first);
        Assert.True(buffer.TryDequeue(out var second));
        Assert.Equal(new ApuStereoSample(3, 4), second);
        Assert.False(buffer.TryDequeue(out var empty));
        Assert.Equal(default, empty);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Enqueue_DropsNewSamplesWhenBufferIsFull()
    {
        var buffer = new AudioRingBuffer(capacity: 2);

        buffer.Enqueue([
            new ApuStereoSample(1, 1),
            new ApuStereoSample(2, 2),
            new ApuStereoSample(3, 3),
        ]);

        Assert.Equal(2, buffer.Count);
        Assert.True(buffer.TryDequeue(out var first));
        Assert.Equal(new ApuStereoSample(1, 1), first);
        Assert.True(buffer.TryDequeue(out var second));
        Assert.Equal(new ApuStereoSample(2, 2), second);
        Assert.False(buffer.TryDequeue(out _));
    }

    [Fact]
    public void Enqueue_WrapsAfterSamplesAreDequeued()
    {
        var buffer = new AudioRingBuffer(capacity: 2);
        buffer.Enqueue([new ApuStereoSample(1, 1), new ApuStereoSample(2, 2)]);

        Assert.True(buffer.TryDequeue(out var first));
        Assert.Equal(new ApuStereoSample(1, 1), first);

        buffer.Enqueue([new ApuStereoSample(3, 3)]);

        Assert.Equal(2, buffer.Count);
        Assert.True(buffer.TryDequeue(out var second));
        Assert.Equal(new ApuStereoSample(2, 2), second);
        Assert.True(buffer.TryDequeue(out var third));
        Assert.Equal(new ApuStereoSample(3, 3), third);
        Assert.False(buffer.TryDequeue(out _));
    }

    [Fact]
    public void Clear_DropsQueuedSamplesButKeepsSamplesEnqueuedAfterClear()
    {
        var buffer = new AudioRingBuffer(capacity: 2);
        buffer.Enqueue([new ApuStereoSample(1, 1), new ApuStereoSample(2, 2)]);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);

        buffer.Enqueue([new ApuStereoSample(3, 3)]);
        Assert.Equal(1, buffer.Count);

        Assert.True(buffer.TryDequeue(out var sample));
        Assert.Equal(new ApuStereoSample(3, 3), sample);
    }
}
