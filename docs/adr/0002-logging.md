# ADR-0002: Use source-generated Microsoft logging with a Serilog file sink

- Status: Accepted
- Date: 2026-07-18

## Context

GBC.Net needs persistent, bounded diagnostic logs that users can attach to support reports. The application already uses `Microsoft.Extensions.Logging` and source-generated `LoggerMessage` methods, but only writes to the debugger. `GbcNet.Core` has no logging dependency.

Direct Serilog use throughout the application or high-frequency hardware logs would add coupling, noise, and runtime cost without improving diagnosis.

## Decision

Application code will use `ILogger<T>` or `ILogger` with source-generated `[LoggerMessage]` methods. Serilog will remain an infrastructure detail providing the file sink.

The logging pipeline will:

- create one process-wide Serilog logger before configuration loading, dependency injection, and database migration, and not replace it during normal startup;
- write text logs containing timestamp with offset, level, category, message, and complete exception;
- write `Warning` or higher under the per-user `logs` directory;
- roll daily or at 2 MiB, retain at most 14 files, and delete rolled files older than 14 days;
- close and flush on normal exit and at the process terminal boundary;
- keep Avalonia trace logging debugger-only.

The composition root may call Serilog directly to create, terminate, and flush the logger. Other code will use Microsoft logging levels: `Warning` for unexpected recoverable failures, `Error` for failed operations or persistence, and `Critical` for process-terminating failures. Serilog maps `Critical` to `Fatal`.

Logging rules:

- log an exception only where it is handled without rethrowing, converted into a user-visible failure, or terminates the process;
- do not log in an intermediate layer that only enriches and rethrows;
- capture unexpected failures from application-owned background operations at their execution boundary;
- do not promote expected validation failures, unsupported input, user cancellation, normal lifecycle, successful persistence, retries, queries, input, frames, instructions, or audio samples to `Warning`;
- do not add ROM paths, user filenames, search text, configuration contents, SRAM, save-state contents, bindings, or SQL to messages or structured properties;
- when useful, identify a ROM only through cartridge metadata;
- keep `GbcNet.Core` and hardware hot paths free of logging.

## Consequences

### Positive

- Application code stays independent of Serilog and its sinks.
- Source generation keeps disabled log paths cheap.
- Startup and runtime failures reach the same bounded user file with stack traces.
- Emulator core performance and dependency boundaries remain unchanged.

### Negative

- Serilog adds two application dependencies.
- `Warning` omits normal session chronology.
- Raw exceptions from the runtime or external libraries may contain paths, filenames, SQL fragments, configuration values, or other user-originated data. GBC.Net will not duplicate these values, but complete redaction would require custom exception formatting.
- Forced termination, operating-system failure, or power loss can prevent the final flush.

## Alternatives considered

- **Call Serilog throughout the application:** rejected to preserve the Microsoft logging abstraction.
- **Implement a custom file provider:** rejected because Serilog already handles rotation, retention, formatting, and flushing.
- **Use only built-in providers:** rejected because they do not provide the required general-purpose rolling file sink.
- **Write debug or information logs to user files:** rejected to avoid an activity log and hot-path noise.
- **Log inside the emulator core:** rejected to preserve performance and separation.

## Revisit criteria

Reconsider if support needs more chronology, logs are uploaded automatically and require strict redaction, multiple processes share the sink, or measured volume requires buffered or asynchronous I/O.
