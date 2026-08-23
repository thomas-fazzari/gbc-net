// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Threading;
using GbcNet.Core.Apu;
using Microsoft.Extensions.Logging;
using SDL;

namespace GbcNet.App.Audio;

/// <summary>
/// Plays emulator audio through an SDL audio stream.
/// </summary>
internal sealed unsafe class SdlAudioOutput : IAudioOutput
{
    private const int Channels = 2;
    private const int SampleRate = 48_000;
    private const int BytesPerFrame = Channels * sizeof(short);
    private const int ConversionFrameCapacity = 512;
    private const int PrebufferFrameCount = SampleRate * 60 / 1000;
    private const int PrebufferByteCount = PrebufferFrameCount * BytesPerFrame;
    private const int MaximumQueuedFrameCount = SampleRate / 2;
    private const int MaximumQueuedByteCount = MaximumQueuedFrameCount * BytesPerFrame;

    private readonly ILogger<SdlAudioOutput> _logger;
    private readonly Lock _streamLock = new();
    private readonly short[] _conversionBuffer = new short[ConversionFrameCapacity * Channels];

    private SDL_AudioStream* _stream;
    private float _gain = 1f;
    private bool _disposed;
    private bool _failureReported;
    private bool _sdlInitialized;
    private bool _started;
    private bool _unavailable;

    public SdlAudioOutput(ILogger<SdlAudioOutput> logger)
    {
        _logger = logger;
        Dispatcher.UIThread.VerifyAccess();

        try
        {
            if (!SDL3.SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO))
            {
                ReportFailure(GetSdlError());
                _unavailable = true;
                return;
            }

            _sdlInitialized = true;
        }
        catch (Exception exception) when (IsNativeInteropException(exception))
        {
            ReportFailure(exception);
            _unavailable = true;
        }
    }

    /// <inheritdoc />
    public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples)
    {
        if (samples.IsEmpty)
        {
            return;
        }

        lock (_streamLock)
        {
            if (_disposed || _unavailable || !_sdlInitialized)
            {
                return;
            }

            try
            {
                if (!EnsureStream())
                {
                    return;
                }

                var queuedByteCount = SDL3.SDL_GetAudioStreamQueued(_stream);
                if (queuedByteCount < 0)
                {
                    DisableStream(GetSdlError());
                    return;
                }

                var frameCount = GetFrameCountToQueue(queuedByteCount, samples.Length);
                var frameOffset = 0;

                while (frameOffset < frameCount)
                {
                    var batchFrameCount = Math.Min(
                        ConversionFrameCapacity,
                        frameCount - frameOffset
                    );
                    var batch = samples.Slice(frameOffset, batchFrameCount);
                    ConvertSamples(batch, _conversionBuffer);

                    fixed (short* buffer = _conversionBuffer)
                    {
                        if (
                            !SDL3.SDL_PutAudioStreamData(
                                _stream,
                                (nint)buffer,
                                batchFrameCount * BytesPerFrame
                            )
                        )
                        {
                            DisableStream(GetSdlError());
                            return;
                        }
                    }

                    frameOffset += batchFrameCount;
                }

                TryStartPlayback();
            }
            catch (Exception exception) when (IsNativeInteropException(exception))
            {
                DisableStream(exception);
            }
        }
    }

    /// <inheritdoc />
    public void SetVolume(int volumePercent, bool muted)
    {
        lock (_streamLock)
        {
            _gain = CalculateGain(volumePercent, muted);

            if (_stream is null || _disposed || _unavailable)
            {
                return;
            }

            try
            {
                if (!SDL3.SDL_SetAudioStreamGain(_stream, _gain))
                {
                    DisableStream(GetSdlError());
                }
            }
            catch (Exception exception) when (IsNativeInteropException(exception))
            {
                DisableStream(exception);
            }
        }
    }

    internal static float CalculateGain(int volumePercent, bool muted)
    {
        var normalizedVolume = Math.Clamp(volumePercent, 0, 100) / 100f;
        return muted ? 0f : normalizedVolume * normalizedVolume;
    }

    internal static void ConvertSamples(
        ReadOnlySpan<ApuStereoSample> samples,
        Span<short> destination
    )
    {
        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            destination[index * Channels] = Saturate(sample.Left);
            destination[(index * Channels) + 1] = Saturate(sample.Right);
        }
    }

    internal static int GetFrameCountToQueue(int queuedByteCount, int requestedFrameCount)
    {
        var availableByteCount = Math.Max(MaximumQueuedByteCount - queuedByteCount, 0);
        return Math.Min(requestedFrameCount, availableByteCount / BytesPerFrame);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_streamLock)
        {
            if (_disposed)
            {
                return;
            }

            _unavailable = !_sdlInitialized;
            _failureReported = !_sdlInitialized;
            _started = false;

            if (_stream is null)
            {
                return;
            }

            try
            {
                if (
                    !SDL3.SDL_PauseAudioStreamDevice(_stream) || !SDL3.SDL_ClearAudioStream(_stream)
                )
                {
                    DisableStream(GetSdlError());
                }
            }
            catch (Exception exception) when (IsNativeInteropException(exception))
            {
                DisableStream(exception);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_streamLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseStream();

            if (!_sdlInitialized)
            {
                return;
            }

            try
            {
                SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO);
            }
            catch (Exception exception) when (IsNativeInteropException(exception))
            {
                ReportFailure(exception);
            }

            _sdlInitialized = false;
        }
    }

    private bool EnsureStream()
    {
        if (_stream is not null)
        {
            return true;
        }

        var spec = new SDL_AudioSpec
        {
            format = SDL3.SDL_AUDIO_S16,
            channels = Channels,
            freq = SampleRate,
        };

        _stream = SDL3.SDL_OpenAudioDeviceStream(
            SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK,
            &spec,
            callback: null,
            userdata: 0
        );
        if (_stream is null || !SDL3.SDL_SetAudioStreamGain(_stream, _gain))
        {
            DisableStream(GetSdlError());
            return false;
        }

        return true;
    }

    private void TryStartPlayback()
    {
        if (_started)
        {
            return;
        }

        switch (SDL3.SDL_GetAudioStreamQueued(_stream))
        {
            case < 0:
                DisableStream(GetSdlError());
                return;
            case >= PrebufferByteCount when SDL3.SDL_ResumeAudioStreamDevice(_stream):
                _started = true;
                break;
            case >= PrebufferByteCount:
                DisableStream(GetSdlError());
                break;
        }
    }

    private void DisableStream(string error)
    {
        ReportFailure(error);
        _unavailable = true;
        _started = false;
        ReleaseStream();
    }

    private void DisableStream(Exception exception)
    {
        ReportFailure(exception);
        _unavailable = true;
        _started = false;
        ReleaseStream();
    }

    private void ReleaseStream()
    {
        var stream = _stream;
        _stream = null;
        _started = false;

        if (stream is null)
        {
            return;
        }

        try
        {
            SDL3.SDL_DestroyAudioStream(stream);
        }
        catch (Exception exception) when (IsNativeInteropException(exception))
        {
            ReportFailure(exception);
        }
    }

    private void ReportFailure(string error)
    {
        if (_failureReported)
        {
            return;
        }

        _failureReported = true;
        SdlAudioOutputLog.AudioPlaybackUnavailable(_logger, error);
    }

    private void ReportFailure(Exception exception)
    {
        if (_failureReported)
        {
            return;
        }

        _failureReported = true;
        SdlAudioOutputLog.AudioPlaybackInteropFailed(_logger, exception);
    }

    private static short Saturate(int value) =>
        (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static bool IsNativeInteropException(Exception exception) =>
        exception
            is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or TypeInitializationException;

    private static string GetSdlError() =>
        SDL3.PtrToStringUTF8(SDL3.Unsafe_SDL_GetError()) ?? "Unknown SDL error";
}

internal static partial class SdlAudioOutputLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SDL audio playback is unavailable: {SdlError}"
    )]
    internal static partial void AudioPlaybackUnavailable(ILogger logger, string sdlError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SDL audio playback is unavailable.")]
    internal static partial void AudioPlaybackInteropFailed(ILogger logger, Exception exception);
}
