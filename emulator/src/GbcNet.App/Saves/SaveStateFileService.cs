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

        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (compressedPayload, payloadHash) = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var compressedPayload = Compress(payload.Span);
                    cancellationToken.ThrowIfCancellationRequested();
                    var payloadHash = SHA256.HashData(payload.Span);
                    cancellationToken.ThrowIfCancellationRequested();
                    return (compressedPayload, payloadHash);
                },
                cancellationToken
            );

            Directory.CreateDirectory(stateDirectoryPath);

            var stream = new FileStream(
                path: temporaryPath,
                mode: FileMode.CreateNew,
                access: FileAccess.Write,
                share: FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough
            );

            await using (stream)
            {
                await stream.WriteAsync(_magic, cancellationToken);
                stream.WriteByte(FormatVersion);
                stream.WriteByte((byte)hardwareModel);
                await stream.WriteAsync(rom.Hash, cancellationToken);
                await WriteInt32Async(stream, payload.Length, cancellationToken);
                await stream.WriteAsync(payloadHash, cancellationToken);
                await WriteInt32Async(stream, compressedPayload.Length, cancellationToken);
                await stream.WriteAsync(compressedPayload, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(sourceFileName: temporaryPath, destFileName: path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                message: "Save-state file could not be written: " + exception.Message,
                innerException: exception
            );
        }
        finally
        {
            _saveLock.Release();
            FileUtils.TryDeleteRegularFile(
                temporaryPath,
                ex => SaveStateFileServiceLog.SaveStateCleanupFailed(logger, ex)
            );
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

            await using (stream)
            {
                var header = await ReadHeaderAsync(stream, rom, hardwareModel, cancellationToken);
                var compressedPayload = new byte[header.CompressedPayloadLength];
                await stream.ReadExactlyAsync(compressedPayload, cancellationToken);

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Save-state file contains trailing data.");
                }

                return await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var payload = Decompress(compressedPayload, header.PayloadLength);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (
                            !CryptographicOperations.FixedTimeEquals(
                                left: SHA256.HashData(payload),
                                right: header.PayloadHash
                            )
                        )
                        {
                            throw new InvalidDataException(
                                "Save-state payload checksum is invalid."
                            );
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        return payload;
                    },
                    cancellationToken
                );
            }
        }
        catch (Exception exception)
            when (exception
                    is FileNotFoundException
                        or InvalidDataException
                        or OperationCanceledException
            )
        {
            throw;
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Save-state file is truncated.", exception);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ZstdSharp.ZstdException
            )
        {
            throw new InvalidDataException("Save-state file could not be read.", exception);
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
        var payload = new byte[payloadLength];
        using var decompressor = new ZstdSharp.Decompressor();
        if (decompressor.Unwrap(compressedPayload, payload) != payloadLength)
        {
            throw new InvalidDataException("Save-state payload length does not match its header.");
        }

        return payload;
    }

    private static async Task<SaveStateHeader> ReadHeaderAsync(
        Stream stream,
        RomStorageIdentity rom,
        HardwareModel hardwareModel,
        CancellationToken cancellationToken
    )
    {
        var magic = new byte[_magic.Length];
        await stream.ReadExactlyAsync(magic, cancellationToken);
        if (!magic.AsSpan().SequenceEqual(_magic))
        {
            throw new InvalidDataException("Save-state file magic is invalid.");
        }

        if (await ReadByteAsync(stream, cancellationToken) != FormatVersion)
        {
            throw new InvalidDataException("Save-state file version is unsupported.");
        }

        if (await ReadByteAsync(stream, cancellationToken) != (byte)hardwareModel)
        {
            throw new InvalidDataException(
                "Save-state hardware model does not match the active game."
            );
        }

        var romHash = new byte[SHA256.HashSizeInBytes];
        await stream.ReadExactlyAsync(romHash, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(left: romHash, right: rom.Hash))
        {
            throw new InvalidDataException("Save-state ROM hash does not match the active game.");
        }

        var payloadLength = await ReadInt32Async(stream, cancellationToken);
        ValidatePayloadLength(payloadLength);

        var payloadHash = new byte[SHA256.HashSizeInBytes];
        await stream.ReadExactlyAsync(payloadHash, cancellationToken);

        var compressedPayloadLength = await ReadInt32Async(stream, cancellationToken);
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
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task<int> ReadInt32Async(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var bytes = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static async Task<byte> ReadByteAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        var value = new byte[1];
        await stream.ReadExactlyAsync(value, cancellationToken);
        return value[0];
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

    private readonly record struct SaveStateHeader(
        int PayloadLength,
        byte[] PayloadHash,
        int CompressedPayloadLength
    );
}

internal static partial class SaveStateFileServiceLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Temporary save-state file cleanup failed.")]
    internal static partial void SaveStateCleanupFailed(ILogger logger, Exception exception);
}
