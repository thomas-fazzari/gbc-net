// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.ExceptionServices;
using GbcNet.Core.Cartridges;

namespace GbcNet.App.Saves;

/// <summary>
/// Captures cartridge save snapshots on the emulation thread and persists
/// the latest snapshot in background.
/// </summary>
internal sealed class CartridgeBatterySaveWriter(
    Cartridge cartridge,
    Func<ReadOnlyMemory<byte>, Task> writeSaveAsync,
    Action<Exception> handleError
)
{
    private readonly Lock _gate = new();
    private Exception? _lastWriteException;
    private byte[]? _pendingSave;
    private Task? _writeTask;

    /// <summary>
    /// Captures dirty cartridge state without waiting for file I/O.
    /// </summary>
    public void QueueSave(bool force = false)
    {
        byte[]? save;
        try
        {
            save = CaptureSave(force);
        }
        catch (InvalidOperationException exception)
        {
            handleError(exception);
            return;
        }

        Enqueue(save);
    }

    /// <summary>
    /// Captures the final state and waits for all queued file writes.
    /// </summary>
    public async Task FlushAsync()
    {
        Enqueue(CaptureSave(force: false));
        await FlushPendingAsync().ConfigureAwait(false);
    }

    internal async Task FlushPendingAsync()
    {
        Task? writeTask;
        lock (_gate)
        {
            StartPendingWrite();
            writeTask = _writeTask;
        }

        if (writeTask is not null)
        {
            await writeTask.ConfigureAwait(false);
        }

        Exception? exception;
        lock (_gate)
        {
            exception = _pendingSave is null ? null : _lastWriteException;
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private byte[]? CaptureSave(bool force)
    {
        if (!force && !cartridge.IsBatterySaveDirty)
        {
            return null;
        }

        var save = cartridge.ExportBatterySave();
        cartridge.ClearBatterySaveDirty();
        return save;
    }

    private void Enqueue(byte[]? save)
    {
        lock (_gate)
        {
            if (save is not null)
            {
                // A newer snapshot replaces any snapshot still waiting to be written.
                _pendingSave = save;
            }

            StartPendingWrite();
        }
    }

    private void StartPendingWrite()
    {
        if (_pendingSave is not null && _writeTask is null)
        {
            _writeTask = Task.Run(WritePendingAsync, CancellationToken.None);
        }
    }

    private async Task WritePendingAsync()
    {
        while (true)
        {
            byte[] save;
            lock (_gate)
            {
                save = _pendingSave!;
                _pendingSave = null;
            }

            try
            {
                await writeSaveAsync(save).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                lock (_gate)
                {
                    // Retain the failed snapshot unless a newer one is already waiting.
                    // The next periodic/final flush retries it without touching cartridge state off-thread.
                    _pendingSave ??= save;
                    _lastWriteException = exception;
                    _writeTask = null;
                }

                handleError(exception);
                return;
            }

            lock (_gate)
            {
                _lastWriteException = null;
                if (_pendingSave is not null)
                {
                    continue;
                }

                _writeTask = null;
                return;
            }
        }
    }
}
