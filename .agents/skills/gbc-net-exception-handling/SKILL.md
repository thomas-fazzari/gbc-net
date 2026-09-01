---
name: gbc-net-exception-handling
description: GBC.Net exception handling rules. Use when adding, removing, or reviewing try/catch, exception translation, cancellation handling, retry policy, or error text in GbcNet.Core or GbcNet.App.
---

# GBC.Net exception handling

## Catch criteria

Catch only when the layer owns recovery, stable contract translation, task or waiter completion, or cleanup that cannot live in `finally`. Do not catch only to log and rethrow, rename a fault, or silence an analyzer.

## Broad catches: who owns them

A broad `catch (Exception)` is allowed only at the outer boundary of an owned long-lived operation. In GBC.Net that means the emulation session loop and `Program.Main`. There, move owned state to a terminal state, complete waiters and tasks, log once, and preserve logger flushing. CA1031 is disabled globally; that is not permission to catch broadly elsewhere.

## Session shutdown model

Owned emulation loops stop through a volatile flag and complete pending operations with `OperationCanceledException`; they do not use a long-lived `CancellationToken`. Keep caller cancellation passed to save-state and cheat-code operations distinct from timeout, session shutdown, I/O, and persistence failures. Do not translate cancellation into a file or persistence fault, and do not log normal-shutdown cancellation.

## Layer contracts

`GbcNet.Core`:

* Never log.
* Throw precise exceptions for corrupt data, such as `InvalidDataException`.
* Treat hardware invariants as bugs, not recoverable failures; never catch merely to hide one.

`GbcNet.App`:

* Translate technical failures only where the caller can act on a stable application-level meaning; preserve the original as `InnerException`.
* Do not log in a service that only enriches and rethrows. Surface user-correctable failures through the normal user-visible error mechanism.

UI: convert handled failures into concise user-visible errors. Do not expose stack traces, SQL, filesystem paths, or other internal details.

Logging contract: see `docs/adr/0002-logging.md` and the `gbc-net-logging` skill.

## Sensitive data

Never add these to repository-authored exception messages: ROM paths, user filenames, save-state contents, SRAM, configuration contents, input bindings, SQL. Runtime or library exception messages may contain such values; do not copy them into repository-authored messages or log templates. Identify a ROM through non-sensitive cartridge metadata when useful.

## Retry policy

Keep retry policy at the operation owner, outside `catch` blocks. Bound attempts, preserve cancellation, and retry only known-transient failures from operations proven safe to repeat.
