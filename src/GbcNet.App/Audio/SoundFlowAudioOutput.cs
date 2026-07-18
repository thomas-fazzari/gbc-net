// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using Microsoft.Extensions.Logging;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace GbcNet.App.Audio;

/// <summary>
/// Plays emulator audio through SoundFlow.
/// </summary>
internal sealed class SoundFlowAudioOutput(ILogger<SoundFlowAudioOutput> logger) : IAudioOutput
{
    private const int Channels = 2;
    private const int SampleRate = 48_000;
    private const int DevicePeriodMilliseconds = 15;
    private const int DevicePeriods = 4;
    private const int PrebufferMilliseconds = 60;
    private const int PrebufferFrameCount = SampleRate * PrebufferMilliseconds / 1000;
    private const int BufferFrameCapacity = SampleRate / 2;
    private const float PcmScale = 32768f;
    private static readonly AudioFormat _audioFormat = new()
    {
        Format = SampleFormat.F32,
        Channels = Channels,
        SampleRate = SampleRate,
    };

    private readonly Lock _deviceLock = new();
    private readonly AudioRingBuffer _buffer = new(BufferFrameCapacity);
    private MiniAudioEngine? _engine;
    private AudioPlaybackDevice? _device;
    private SoundFlowSampleSource? _source;
    private int _isDeviceCreated;
    private int _isDisposed;
    private int _isStarted;
    private int _needsPrebuffer = 1;
    private int _isUnavailable;

    /// <inheritdoc />
    public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples)
    {
        if (
            samples.IsEmpty
            || Volatile.Read(ref _isDisposed) != 0
            || Volatile.Read(ref _isUnavailable) != 0
            || !EnsureDeviceCreated()
        )
        {
            return;
        }

        _buffer.Enqueue(samples);
        TryStartPlayback();
    }

    /// <inheritdoc />
    public void Clear()
    {
        Clear(resetUnavailable: true);
    }

    private void Clear(bool resetUnavailable)
    {
        if (resetUnavailable)
        {
            Volatile.Write(location: ref _isUnavailable, value: 0);
        }

        _buffer.Clear();
        Volatile.Write(location: ref _needsPrebuffer, value: 1);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(location1: ref _isDisposed, value: 1) != 0)
        {
            return;
        }

        Clear(resetUnavailable: false);

        lock (_deviceLock)
        {
            ReleaseDevice();
        }
    }

    private bool EnsureDeviceCreated()
    {
        if (Volatile.Read(ref _isDeviceCreated) != 0)
        {
            return true;
        }

        lock (_deviceLock)
        {
            if (Volatile.Read(ref _isDeviceCreated) != 0)
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
                    new MiniAudioDeviceConfig
                    {
                        PeriodSizeInMilliseconds = DevicePeriodMilliseconds,
                        Periods = DevicePeriods,
                    }
                );
                _device.MasterMixer.AddComponent(_source);
                Volatile.Write(location: ref _isDeviceCreated, value: 1);
                return true;
            }
            catch (Exception exception) when (IsExpectedAudioStartupException(exception))
            {
                DisableAudioCore();
                return false;
            }
        }
    }

    private void TryStartPlayback()
    {
        if (
            Volatile.Read(ref _isStarted) != 0
            || Volatile.Read(ref _isDisposed) != 0
            || Volatile.Read(ref _isUnavailable) != 0
            || _buffer.Count < PrebufferFrameCount
        )
        {
            return;
        }

        lock (_deviceLock)
        {
            if (Volatile.Read(ref _isStarted) != 0 || _device is null)
            {
                return;
            }

            // Start with queued audio so slower hosts do not underrun immediately
            _device.Start();
            Volatile.Write(location: ref _isStarted, value: 1);
        }
    }

    private void DisableAudioCore()
    {
        // Called while _deviceLock is held during startup failure handling
        Volatile.Write(location: ref _isUnavailable, value: 1);
        Clear(resetUnavailable: false);
        ReleaseDevice();
    }

    private void ReleaseDevice()
    {
        var device = _device;
        var source = _source;
        var engine = _engine;
        var wasStarted = Volatile.Read(ref _isStarted) != 0;

        _device = null;
        _source = null;
        _engine = null;
        Volatile.Write(location: ref _isDeviceCreated, value: 0);
        Volatile.Write(location: ref _isStarted, value: 0);
        Volatile.Write(location: ref _needsPrebuffer, value: 1);
        if (device is not null)
        {
            if (wasStarted)
            {
                try
                {
                    device.Stop();
                }
                catch (Exception exception) when (IsExpectedAudioStartupException(exception))
                {
                    Volatile.Write(location: ref _isUnavailable, value: 1);
                    SoundFlowAudioOutputLog.PlaybackDeviceReleaseFailed(logger, exception);
                }
            }

            if (source is not null)
            {
                try
                {
                    device.MasterMixer.RemoveComponent(source);
                }
                catch (Exception exception) when (IsExpectedAudioStartupException(exception))
                {
                    Volatile.Write(location: ref _isUnavailable, value: 1);
                    SoundFlowAudioOutputLog.PlaybackDeviceReleaseFailed(logger, exception);
                }
            }

            try
            {
                device.Dispose();
            }
            catch (Exception exception) when (IsExpectedAudioStartupException(exception))
            {
                Volatile.Write(location: ref _isUnavailable, value: 1);
                SoundFlowAudioOutputLog.PlaybackDeviceReleaseFailed(logger, exception);
            }
        }

        try
        {
            source?.Dispose();
            engine?.Dispose();
        }
        catch (Exception exception) when (IsExpectedAudioStartupException(exception))
        {
            Volatile.Write(location: ref _isUnavailable, value: 1);
            SoundFlowAudioOutputLog.AudioEngineReleaseFailed(logger, exception);
        }
    }

    private void Fill(Span<float> output, int channels)
    {
        if (channels != Channels || Volatile.Read(ref _isDisposed) != 0)
        {
            output.Clear();
            return;
        }

        var requestedFrames = output.Length / Channels;

        if (Volatile.Read(ref _needsPrebuffer) != 0)
        {
            if (_buffer.Count < PrebufferFrameCount)
            {
                output.Clear();
                return;
            }

            Volatile.Write(location: ref _needsPrebuffer, value: 0);
        }

        var frame = 0;

        for (; frame < requestedFrames && _buffer.TryDequeue(out var sample); frame++)
        {
            output[frame * Channels] = sample.Left / PcmScale;
            output[(frame * Channels) + 1] = sample.Right / PcmScale;
        }

        output[(frame * Channels)..].Clear();
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

internal static partial class SoundFlowAudioOutputLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "SoundFlow playback device release failed.")]
    internal static partial void PlaybackDeviceReleaseFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SoundFlow audio engine release failed.")]
    internal static partial void AudioEngineReleaseFailed(ILogger logger, Exception exception);
}
