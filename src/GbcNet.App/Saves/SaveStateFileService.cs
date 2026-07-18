// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Saves;

/// <summary>
/// Persists versioned, ROM-bound emulator save-state payloads atomically.
/// </summary>
internal sealed class SaveStateFileService(
    string stateDirectoryPath,
    ILogger<SaveStateFileService> logger
) : IDisposable
{
    private static readonly byte[] _magic = "GBCNETST"u8.ToArray();

    private const byte FormatVersion = 1;
    private const string FileExtension = ".gbstate";
    private const int MaximumPayloadLength = 64 * 1024 * 1024;
    private const int MaximumCompressedPayloadLength = MaximumPayloadLength + (1024 * 1024);

    private readonly SemaphoreSlim _saveLock = new(initialCount: 1, maxCount: 1);

    public async Task SaveAsync(
        RomStorageIdentity rom,
        int slot,
        HardwareModel hardwareModel,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(rom);

        ValidateSlot(slot);
        ValidateHardwareModel(hardwareModel);
        ValidatePayloadLength(payload.Length);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetSaveStatePath(rom, slot);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        await _saveLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compressedPayload = await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Compress(payload.Span);
                    },
                    cancellationToken
                )
                .ConfigureAwait(continueOnCapturedContext: false);

            Directory.CreateDirectory(stateDirectoryPath);

            var stream = new FileStream(
                path: temporaryPath,
                mode: FileMode.CreateNew,
                access: FileAccess.Write,
                share: FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough
            );

            await using (stream.ConfigureAwait(continueOnCapturedContext: false))
            {
                await stream
                    .WriteAsync(_magic, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                stream.WriteByte(FormatVersion);
                stream.WriteByte((byte)hardwareModel);
                await stream
                    .WriteAsync(rom.Hash, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                await WriteInt32Async(stream, payload.Length, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                await stream
                    .WriteAsync(SHA256.HashData(payload.Span), cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                await WriteInt32Async(stream, compressedPayload.Length, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                await stream
                    .WriteAsync(compressedPayload, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                await stream
                    .FlushAsync(cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(sourceFileName: temporaryPath, destFileName: path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveStateFileServiceLog.SaveStateWriteFailed(logger, exception);
            throw new IOException(
                message: "Save-state file could not be written: " + exception.Message,
                innerException: exception
            );
        }
        finally
        {
            _saveLock.Release();
            TryDelete(temporaryPath);
        }
    }

    public void Dispose() => _saveLock.Dispose();

    public async Task<byte[]> LoadAsync(
        RomStorageIdentity rom,
        int slot,
        HardwareModel hardwareModel,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(rom);

        ValidateSlot(slot);
        ValidateHardwareModel(hardwareModel);

        var path = GetSaveStatePath(rom, slot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                message: "Save-state slot does not exist.",
                fileName: path
            );
        }

        try
        {
            var stream = new FileStream(
                path: path,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan
            );

            await using (stream.ConfigureAwait(continueOnCapturedContext: false))
            {
                var header = await ReadHeaderAsync(stream, rom, hardwareModel, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                var compressedPayload = new byte[header.CompressedPayloadLength];
                await ReadExactlyAsync(stream, compressedPayload, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Save-state file contains trailing data.");
                }

                var payload = Decompress(compressedPayload, header.PayloadLength);
                if (
                    !CryptographicOperations.FixedTimeEquals(
                        left: SHA256.HashData(payload),
                        right: header.PayloadHash
                    )
                )
                {
                    throw new InvalidDataException("Save-state payload checksum is invalid.");
                }

                return payload;
            }
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            SaveStateFileServiceLog.SaveStateReadFailed(logger, exception);
            throw new InvalidDataException(
                message: "Save-state file could not be read: " + exception.Message,
                innerException: exception
            );
        }
    }

    internal string GetSaveStatePath(RomStorageIdentity rom, int slot)
    {
        ArgumentNullException.ThrowIfNull(rom);
        ValidateSlot(slot);
        return Path.Combine(
            path1: stateDirectoryPath,
            path2: string.Concat(
                str0: rom.FileStem,
                str1: ".slot-",
                str2: slot.ToString(CultureInfo.InvariantCulture),
                str3: FileExtension
            )
        );
    }

    internal DateTime? GetSaveStateDate(RomStorageIdentity rom, int slot)
    {
        var path = GetSaveStatePath(rom, slot);
        return File.Exists(path) ? File.GetLastWriteTime(path) : null;
    }

    private static byte[] Compress(ReadOnlySpan<byte> payload)
    {
        using var compressor = new ZstdSharp.Compressor();
        return compressor.Wrap(payload).ToArray();
    }

    private static byte[] Decompress(ReadOnlySpan<byte> compressedPayload, int payloadLength)
    {
        using var decompressor = new ZstdSharp.Decompressor();
        var payload = decompressor.Unwrap(compressedPayload, payloadLength);
        if (payload.Length != payloadLength)
        {
            throw new InvalidDataException("Save-state payload length does not match its header.");
        }

        return payload.ToArray();
    }

    private static async Task<SaveStateHeader> ReadHeaderAsync(
        Stream stream,
        RomStorageIdentity rom,
        HardwareModel hardwareModel,
        CancellationToken cancellationToken
    )
    {
        var magic = new byte[_magic.Length];
        await ReadExactlyAsync(stream, magic, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (!magic.AsSpan().SequenceEqual(_magic))
        {
            throw new InvalidDataException("Save-state file magic is invalid.");
        }

        if (
            await ReadByteAsync(stream, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false) != FormatVersion
        )
        {
            throw new InvalidDataException("Save-state file version is unsupported.");
        }

        if (
            await ReadByteAsync(stream, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false) != (byte)hardwareModel
        )
        {
            throw new InvalidDataException(
                "Save-state hardware model does not match the active game."
            );
        }

        var romHash = new byte[SHA256.HashSizeInBytes];
        await ReadExactlyAsync(stream, romHash, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (!CryptographicOperations.FixedTimeEquals(left: romHash, right: rom.Hash))
        {
            throw new InvalidDataException("Save-state ROM hash does not match the active game.");
        }

        var payloadLength = await ReadInt32Async(stream, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        ValidatePayloadLength(payloadLength);

        var payloadHash = new byte[SHA256.HashSizeInBytes];
        await ReadExactlyAsync(stream, payloadHash, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var compressedPayloadLength = await ReadInt32Async(stream, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (compressedPayloadLength is < 0 or > MaximumCompressedPayloadLength)
        {
            throw new InvalidDataException("Save-state compressed payload length is invalid.");
        }

        return new SaveStateHeader(
            PayloadLength: payloadLength,
            PayloadHash: payloadHash,
            CompressedPayloadLength: compressedPayloadLength
        );
    }

    private static async Task WriteInt32Async(
        Stream stream,
        int value,
        CancellationToken cancellationToken
    )
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        await stream
            .WriteAsync(bytes, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private static async Task<int> ReadInt32Async(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var bytes = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, bytes, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static async Task<byte> ReadByteAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var value = new byte[1];
        await ReadExactlyAsync(stream, value, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return value[0];
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken
    )
    {
        while (!destination.IsEmpty)
        {
            var count = await stream
                .ReadAsync(destination, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (count == 0)
            {
                throw new InvalidDataException("Save-state file is truncated.");
            }

            destination = destination[count..];
        }
    }

    private static void ValidatePayloadLength(int payloadLength)
    {
        if (payloadLength is < 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException("Save-state payload length is invalid.");
        }
    }

    private static void ValidateSlot(int slot)
    {
        if (slot < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(slot),
                actualValue: slot,
                message: "Save-state slot must be nonnegative."
            );
        }
    }

    private static void ValidateHardwareModel(HardwareModel hardwareModel)
    {
        if (!Enum.IsDefined(hardwareModel))
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(hardwareModel),
                actualValue: hardwareModel,
                message: "Save-state hardware model is invalid."
            );
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveStateFileServiceLog.SaveStateCleanupFailed(logger, path, exception);
        }
    }

    private readonly record struct SaveStateHeader(
        int PayloadLength,
        byte[] PayloadHash,
        int CompressedPayloadLength
    );
}

internal static partial class SaveStateFileServiceLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Save-state file write failed.")]
    internal static partial void SaveStateWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Save-state file read failed.")]
    internal static partial void SaveStateReadFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Temporary save-state file cleanup failed for {Path}."
    )]
    internal static partial void SaveStateCleanupFailed(
        ILogger logger,
        string path,
        Exception exception
    );
}
