using GbcNet.Core.Apu;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace GbcNet.Gui.Audio;

/// <summary>
/// Plays emulator audio through SoundFlow.
/// </summary>
internal sealed class SoundFlowAudioOutput : IAudioOutput
{
    private const int Channels = 2;
    private const int SampleRate = 48_000;
    private const int BufferFrameCapacity = SampleRate / 2;
    private const float PcmScale = 32768f;
    private static readonly AudioFormat _audioFormat = new()
    {
        Format = SampleFormat.F32,
        Channels = Channels,
        SampleRate = SampleRate,
    };

    private readonly Lock _bufferLock = new();
    private readonly Lock _deviceLock = new();
    private readonly ApuStereoSample[] _buffer = new ApuStereoSample[BufferFrameCapacity];
    private MiniAudioEngine? _engine;
    private AudioPlaybackDevice? _device;
    private SoundFlowSampleSource? _source;
    private int _bufferStart;
    private int _bufferCount;
    private int _isDisposed;
    private int _isStarted;
    private int _isUnavailable;

    /// <inheritdoc />
    public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples)
    {
        if (
            samples.IsEmpty
            || Volatile.Read(ref _isDisposed) != 0
            || Volatile.Read(ref _isUnavailable) != 0
            || !TryStart()
        )
        {
            return;
        }

        lock (_bufferLock)
        {
            foreach (ApuStereoSample sample in samples)
            {
                if (_bufferCount == _buffer.Length)
                {
                    _bufferStart = (_bufferStart + 1) % _buffer.Length;
                    _bufferCount--;
                }

                _buffer[(_bufferStart + _bufferCount) % _buffer.Length] = sample;
                _bufferCount++;
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_bufferLock)
        {
            _bufferStart = 0;
            _bufferCount = 0;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Clear();

        lock (_deviceLock)
        {
            ReleaseDevice();
        }
    }

    private bool TryStart()
    {
        if (Volatile.Read(ref _isStarted) != 0)
        {
            return true;
        }

        lock (_deviceLock)
        {
            if (Volatile.Read(ref _isStarted) != 0)
            {
                return true;
            }

            if (Volatile.Read(ref _isDisposed) != 0 || Volatile.Read(ref _isUnavailable) != 0)
            {
                return false;
            }

            try
            {
                _engine = new MiniAudioEngine();
                _source = new SoundFlowSampleSource(_engine, _audioFormat, this);
                _device = _engine.InitializePlaybackDevice(
                    deviceInfo: null,
                    _audioFormat,
                    new MiniAudioDeviceConfig { PeriodSizeInMilliseconds = 10, Periods = 3 }
                );
                _device.MasterMixer.AddComponent(_source);
                _device.Start();
                Volatile.Write(ref _isStarted, 1);
                return true;
            }
            catch (Exception exception) when (IsExpectedAudioStartupException(exception))
            {
                DisableAudio();
                return false;
            }
        }
    }

    private void DisableAudio()
    {
        Volatile.Write(ref _isUnavailable, 1);
        Clear();
        ReleaseDevice();
    }

    private void ReleaseDevice()
    {
        AudioPlaybackDevice? device = _device;
        SoundFlowSampleSource? source = _source;
        MiniAudioEngine? engine = _engine;

        _device = null;
        _source = null;
        _engine = null;
        Volatile.Write(ref _isStarted, 0);

        if (device is not null)
        {
            device.Stop();

            if (source is not null)
            {
                device.MasterMixer.RemoveComponent(source);
            }

            device.Dispose();
        }

        source?.Dispose();
        engine?.Dispose();
    }

    private void Fill(Span<float> output, int channels)
    {
        if (channels != Channels || Volatile.Read(ref _isDisposed) != 0)
        {
            output.Clear();
            return;
        }

        lock (_bufferLock)
        {
            int frames = Math.Min(output.Length / Channels, _bufferCount);

            for (int frame = 0; frame < frames; frame++)
            {
                ApuStereoSample sample = _buffer[(_bufferStart + frame) % _buffer.Length];
                output[frame * Channels] = sample.Left / PcmScale;
                output[(frame * Channels) + 1] = sample.Right / PcmScale;
            }

            _bufferStart = (_bufferStart + frames) % _buffer.Length;
            _bufferCount -= frames;

            if (_bufferCount == 0)
            {
                _bufferStart = 0;
            }

            output[(frames * Channels)..].Clear();
        }
    }

    private static bool IsExpectedAudioStartupException(Exception exception) =>
        exception
            is MiniaudioException
                or DllNotFoundException
                or BadImageFormatException
                or TypeInitializationException
                or InvalidOperationException
                or NotSupportedException
                or UnauthorizedAccessException;

    /// <summary>
    /// Pulls queued emulator samples from the owning output when SoundFlow renders audio.
    /// </summary>
    private sealed class SoundFlowSampleSource(
        AudioEngine engine,
        AudioFormat format,
        SoundFlowAudioOutput audioOutput
    ) : SoundComponent(engine, format)
    {
        protected override void GenerateAudio(Span<float> buffer, int channels)
        {
            audioOutput.Fill(buffer, channels);
        }
    }
}
