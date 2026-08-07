# GBC.Net exception handling

## Find the owner first

Before adding a `catch`, identify:

1. the operation owner;
2. the action that layer can take.

Catch only to:

* recover while leaving valid state;
* translate once into this layer's stable contract;
* complete owned state, waiters, or tasks;
* add stable context that materially helps diagnose the fault;
* perform cleanup that cannot live in `finally`.

Otherwise, let the exception flow to the owner.

Do not catch only to:

* log and rethrow;
* rename the fault;
* restore a value that is already the default;
* make a method appear safe;
* silence an analyzer.

Use standard guard exceptions for invalid arguments.

Do not turn bugs, cancellation, corrupt data, or unexpected system faults into expected results.

## Catch the exact failure set

Catch the most specific exception types that share the same meaning and action.

Do not use `catch (Exception)` as shorthand for a guessed list.

Merge exception types when their handling is identical:

```csharp
catch (Exception exception)
    when (exception is IOException or UnauthorizedAccessException)
{
    throw new IOException("Save file could not be read.", exception);
}
```

Use a shared base type when it selects exactly the intended failure set.

Keep catches separate when their:

* meaning;
* logging;
* recovery;
* cleanup;
* retry policy;
* resulting state

differs.

Never group cancellation with file, persistence, or data faults.

A broad catch is allowed only at the outer boundary of an owned long-lived operation or process. The emulation session loop and `Program.Main` may observe every unexpected fault there.

At such a boundary:

* move owned state to a terminal state;
* complete owned waiters or tasks;
* log the unexpected exception once;
* preserve required cleanup and logger flushing.

CA1031 is disabled globally. Do not use that as permission to catch broadly elsewhere.

Never leave a `catch` empty. A comment is not exception handling.

Repeated catches for `ObjectDisposedException` usually indicate unclear lifetime ownership. Fix the ownership model when possible.

## Keep cancellation distinct

Cancellation is control flow only when the relevant owned token or shutdown mechanism caused it.

The emulation session has no long-lived `CancellationToken`. It stops through the volatile `_isStopped` flag and completes pending operations with `OperationCanceledException`. Match that model.

```csharp
catch (OperationCanceledException)
{
    return;
}
```

Keep caller cancellation passed to save-state and cheat-code operations distinct from:

* timeout;
* session shutdown;
* I/O failure;
* persistence failure.

Do not turn cancellation into success unless cancellation is the documented successful outcome.

Do not translate cancellation into a file or persistence fault.

Do not catch `TaskCanceledException` separately unless an external API contract specifically requires it.

When linked tokens can end for different reasons, inspect the relevant tokens before deciding what the cancellation means.

## Keep layer contracts clear

### `GbcNet.Core`

* Never log.
* Throw precise exceptions for corrupt data, such as `InvalidDataException`.
* Use guard exceptions for invalid arguments.
* Treat hardware invariants as bugs, not recoverable failures.
* Never catch merely to hide a hardware failure.

### `GbcNet.App`

* Translate technical failures, such as file I/O or SQLite errors, only where the caller can act on a stable application-level meaning.
* Preserve the original exception as `InnerException` when a wrapper is necessary.
* Do not log in a service that only enriches and rethrows.
* Surface expected user-correctable failures through the application's normal user-visible error mechanism.

### Composition root and owned long-lived operations

* Broad catches are allowed only at their outer boundary.
* Log unexpected failures once.
* Move owned state to a terminal state.
* Complete owned tasks and waiters.
* Preserve process logger flushing.

### UI

* Convert handled failures into concise user-visible errors.
* Do not expose stack traces, SQL, filesystem paths, or other internal implementation details.

## Log or translate once

Log where the consequence is owned.

Do not catch, log, and rethrow when an outer owner will log the same fault.

Use:

```csharp
throw;
```

Never use:

```csharp
throw exception;
```

Do not wrap an exception only to change its message.

Use source-generated `[LoggerMessage]` methods with the owner's injected `ILogger<T>`.

Pass the exception directly to the generated method.

Do not add an `EventId`.

Do not log normal shutdown cancellation.

Follow `docs/adr/0002-logging.md` for the complete logging contract.

## Protect sensitive data

Never add these values to repository-authored exception messages:

* ROM paths;
* user filenames;
* save-state contents;
* SRAM;
* configuration contents;
* input bindings;
* SQL.

Preserve the source exception as `InnerException` when required. Runtime or external-library exception messages may contain sensitive values; do not copy those values into repository-authored messages, log templates, or structured properties.

When useful, identify a ROM only through non-sensitive cartridge metadata.

## Write useful error text

Apply the `## Writing` rules from `.omp/RULES.md` to:

* exception messages;
* log templates;
* comments;
* XML documentation.

Use short, complete sentences and common words.

State the failed action. Add only stable context that helps the caller act or helps the owner identify the fault.

Remove:

* filler;
* repeated facts;
* unnecessary parentheses;
* semicolons;
* implementation narration.

Spell out uncommon abbreviations on first use.

Never use "hand-written" as a quality label.

Explain why a catch is safe only when the code cannot make that clear.

Prefer:

```csharp
throw new InvalidDataException(
    "Save-state payload checksum is invalid.");
```

Avoid:

```csharp
throw new InvalidDataException(
    "Something went wrong.");
```

Do not repeat the exception type or information already represented by structured log properties.

## Clean up without hiding the cause

Put unconditional resource release in `finally` or the owner's idempotent `DisposeAsync`.

For owned asynchronous resources:

1. stop or cancel producers;
2. await owned loops;
3. dispose resources.

Do not dispose a reader, writer, stream, or other resource while its owner can still use it.

Observe secondary cleanup failures.

Log them at the outer owner when their consequence warrants logging.

Do not replace a more precise primary failure unless the cleanup failure changes the real operation outcome.

The emulation session captures a final-save exception in `finally` and rethrows it after the loop exits. Follow that ownership pattern where applicable.

## Keep retry policy outside catches

Do not implement retry policy inside a `catch`.

Put retry policy at the operation owner.

A retry policy must:

* have bounded attempts;
* preserve cancellation;
* retry only failures known to be transient;
* retry only operations proven safe to repeat.

Logging a retry is normally unnecessary. Log the final degraded or failed operation according to its consequence.

## Review each catch

Before keeping a `catch`, verify that:

1. The protected call can throw the caught type.
2. This layer owns the resulting action.
3. The caught set contains exactly the failures with that meaning.
4. Failures with different meanings remain separate.
5. Cancellation preserves its source and meaning.
6. The exception is logged at most once.
7. Owned waiters and tasks complete exactly once.
8. Cleanup is awaited and cannot race its owner.
9. Repository-authored error text follows the writing rules.
10. Sensitive values are not copied into messages or structured logging properties.
11. The catch does more than silence an analyzer.
