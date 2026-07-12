// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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
    private byte[]? _pendingSave;
    private Task? _writeTask;

    /// <summary>
    /// Captures dirty cartridge state without waiting for file I/O.
    /// </summary>
    public void QueueSave(bool force = false)
    {
        byte[]? save = null;

        try
        {
            if (force || cartridge.IsBatterySaveDirty)
            {
                save = cartridge.ExportBatterySave();
                cartridge.ClearBatterySaveDirty();
            }
        }
        catch (InvalidOperationException exception)
        {
            handleError(exception);
        }

        lock (_gate)
        {
            if (save is not null)
            {
                // A newer snapshot replaces any snapshot still waiting to be written.
                _pendingSave = save;
            }

            if (_pendingSave is not null && _writeTask is null)
            {
                _writeTask = Task.Run(WritePendingAsync, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Captures the final state and waits for all queued file writes.
    /// </summary>
    public async Task FlushAsync()
    {
        QueueSave();

        Task? writeTask;
        lock (_gate)
        {
            writeTask = _writeTask;
        }

        if (writeTask is not null)
        {
            await writeTask.ConfigureAwait(false);
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
                    _writeTask = null;
                }

                handleError(exception);
                return;
            }

            lock (_gate)
            {
                if (_pendingSave is null)
                {
                    _writeTask = null;
                    return;
                }
            }
        }
    }
}
