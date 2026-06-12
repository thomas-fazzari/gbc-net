using GbcNet.Core.Apu;

namespace GbcNet.Gui.Audio;

/// <summary>
/// Single-producer/single-consumer ring buffer for audio samples.
/// </summary>
internal sealed class AudioRingBuffer
{
    private readonly ApuStereoSample[] _samples;
    private long _readCursor;
    private long _writeCursor;

    internal AudioRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _samples = new ApuStereoSample[capacity];
    }

    /// <summary>
    /// Number of queued sample frames visible to the audio output.
    /// </summary>
    internal int Count
    {
        get
        {
            var count = Volatile.Read(ref _writeCursor) - Volatile.Read(ref _readCursor);
            return (int)Math.Clamp(count, 0, _samples.Length);
        }
    }

    /// <summary>
    /// Adds samples from the emulation thread, dropping new samples when the buffer is full.
    /// </summary>
    internal void Enqueue(ReadOnlySpan<ApuStereoSample> samples)
    {
        var writeCursor = Volatile.Read(ref _writeCursor);
        var readCursor = Volatile.Read(ref _readCursor);

        foreach (var sample in samples)
        {
            if (writeCursor - readCursor >= _samples.Length)
            {
                readCursor = Volatile.Read(ref _readCursor);
                if (writeCursor - readCursor >= _samples.Length)
                {
                    break;
                }
            }

            _samples[(int)(writeCursor % _samples.Length)] = sample;
            writeCursor++;
        }

        Volatile.Write(ref _writeCursor, writeCursor);
    }

    /// <summary>
    /// Reads one sample from the audio callback thread.
    /// </summary>
    internal bool TryDequeue(out ApuStereoSample sample)
    {
        var writeCursor = Volatile.Read(ref _writeCursor);
        var readCursor = Volatile.Read(ref _readCursor);

        if (readCursor >= writeCursor)
        {
            sample = default;
            return false;
        }

        sample = _samples[(int)(readCursor % _samples.Length)];
        Volatile.Write(ref _readCursor, readCursor + 1);
        return true;
    }

    /// <summary>
    /// Drops queued samples without reallocating the backing buffer.
    /// </summary>
    internal void Clear()
    {
        Volatile.Write(ref _readCursor, Volatile.Read(ref _writeCursor));
    }
}
