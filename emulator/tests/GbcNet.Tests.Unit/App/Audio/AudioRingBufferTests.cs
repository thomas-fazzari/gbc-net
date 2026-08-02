// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
using GbcNet.Core.Apu;

namespace GbcNet.Tests.Unit.App.Audio;

public sealed class AudioRingBufferTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        FluentActions
            .Invoking(() => new AudioRingBuffer(0))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        FluentActions
            .Invoking(() => new AudioRingBuffer(-1))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EnqueueAndTryDequeue_PreserveSampleOrder()
    {
        var buffer = new AudioRingBuffer(capacity: 3);

        buffer.Enqueue([new ApuStereoSample(1, 2), new ApuStereoSample(3, 4)]);

        buffer.Count.Should().Be(2);
        buffer.TryDequeue(out var first).Should().BeTrue();
        first.Should().Be(new ApuStereoSample(1, 2));
        buffer.TryDequeue(out var second).Should().BeTrue();
        second.Should().Be(new ApuStereoSample(3, 4));
        buffer.TryDequeue(out var empty).Should().BeFalse();
        empty.Should().Be(default(ApuStereoSample));
        buffer.Count.Should().Be(0);
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

        buffer.Count.Should().Be(2);
        buffer.TryDequeue(out var first).Should().BeTrue();
        first.Should().Be(new ApuStereoSample(1, 1));
        buffer.TryDequeue(out var second).Should().BeTrue();
        second.Should().Be(new ApuStereoSample(2, 2));
        buffer.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void Enqueue_WrapsAfterSamplesAreDequeued()
    {
        var buffer = new AudioRingBuffer(capacity: 2);
        buffer.Enqueue([new ApuStereoSample(1, 1), new ApuStereoSample(2, 2)]);

        buffer.TryDequeue(out var first).Should().BeTrue();
        first.Should().Be(new ApuStereoSample(1, 1));

        buffer.Enqueue([new ApuStereoSample(3, 3)]);

        buffer.Count.Should().Be(2);
        buffer.TryDequeue(out var second).Should().BeTrue();
        second.Should().Be(new ApuStereoSample(2, 2));
        buffer.TryDequeue(out var third).Should().BeTrue();
        third.Should().Be(new ApuStereoSample(3, 3));
        buffer.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_DropsQueuedSamplesButKeepsSamplesEnqueuedAfterClear()
    {
        var buffer = new AudioRingBuffer(capacity: 2);
        buffer.Enqueue([new ApuStereoSample(1, 1), new ApuStereoSample(2, 2)]);

        buffer.Clear();

        buffer.Count.Should().Be(0);

        buffer.Enqueue([new ApuStereoSample(3, 3)]);
        buffer.Count.Should().Be(1);

        buffer.TryDequeue(out var sample).Should().BeTrue();
        sample.Should().Be(new ApuStereoSample(3, 3));
    }
}
