namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Converts elapsed APU T-cycles into fixed-rate stereo sample slots without floating-point drift.
/// </summary>
internal sealed class SampleBuffer
{
    internal const int DefaultSampleRate = 48_000;
    internal const int DmgClockHz = 4_194_304;
    internal const int DefaultCapacity = DefaultSampleRate / 10;

    private readonly ApuStereoSample[] _samples;
    private readonly int _sampleRate;
    private int _start;
    private long _accumulator;

    public SampleBuffer(int sampleRate = DefaultSampleRate, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _sampleRate = sampleRate;
        _samples = new ApuStereoSample[capacity];
    }

    /// <summary>
    /// Number of samples waiting to be drained.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Advances sample timing and returns how many mixer samples are due.
    /// </summary>
    public int Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        _accumulator += (long)tCycles * _sampleRate;
        int samplesDue = 0;

        while (_accumulator >= DmgClockHz)
        {
            samplesDue++;
            _accumulator -= DmgClockHz;
        }

        return samplesDue;
    }

    /// <summary>
    /// Buffers one mixed stereo sample, dropping the oldest sample if full.
    /// </summary>
    public void Add(ApuStereoSample sample)
    {
        if (Count == _samples.Length)
        {
            _samples[_start] = sample;
            _start = (_start + 1) % _samples.Length;
            return;
        }

        _samples[(_start + Count) % _samples.Length] = sample;
        Count++;
    }

    /// <summary>
    /// Drains buffered samples in playback order, preserving samples that do not fit.
    /// </summary>
    public int Drain(Span<ApuStereoSample> destination)
    {
        int drained = Math.Min(destination.Length, Count);

        for (int index = 0; index < drained; index++)
        {
            destination[index] = _samples[(_start + index) % _samples.Length];
        }

        _start = (_start + drained) % _samples.Length;
        Count -= drained;
        if (Count == 0)
        {
            _start = 0;
        }

        return drained;
    }
}
