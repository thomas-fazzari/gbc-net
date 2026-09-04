// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GbcNet.Tests.Integration;

/// <summary>
/// Fails every synchronous and asynchronous save to exercise rollback paths.
/// </summary>
/// <param name="exception">The exception to throw, or a synthetic database error when omitted.</param>
internal sealed class FailingSaveChangesInterceptor(Exception? exception = null)
    : SaveChangesInterceptor
{
    /// <summary>
    /// Gets an interceptor that throws a synthetic <see cref="DbUpdateException"/>.
    /// </summary>
    public static FailingSaveChangesInterceptor Instance { get; } = new();

    private readonly Exception _exception =
        exception ?? new DbUpdateException("Synthetic save failure.");

    /// <summary>
    /// Throws the configured exception before a synchronous save.
    /// </summary>
    /// <exception cref="Exception">Always throws the configured exception.</exception>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    ) => throw _exception;

    /// <summary>
    /// Throws the configured exception before an asynchronous save.
    /// </summary>
    /// <exception cref="Exception">Always throws the configured exception.</exception>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    ) => throw _exception;
}
