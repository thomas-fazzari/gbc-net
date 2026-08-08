// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GbcNet.Tests.Integration;

/// <summary>
/// <see cref="SaveChangesInterceptor"/> that throws a configurable exception on every save,
/// for testing rollback/failure paths. Overrides both sync and async paths.
/// The default <see cref="Instance"/> throws <see cref="DbUpdateException"/>. A custom
/// exception can be passed to the constructor for tests that expect a different type or message.
/// </summary>
internal sealed class FailingSaveChangesInterceptor(Exception? exception = null)
    : SaveChangesInterceptor
{
    public static FailingSaveChangesInterceptor Instance { get; } = new();

    private readonly Exception _exception =
        exception ?? new DbUpdateException("Synthetic save failure.");

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    ) => throw _exception;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    ) => throw _exception;
}
