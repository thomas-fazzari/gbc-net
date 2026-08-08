---
name: gbc-net-logging
description: GBC.Net logging contract per ADR 0002. Use when adding or reviewing logging in GbcNet.App — source-generated LoggerMessage methods, ILogger injection, provider boundaries, Serilog composition root.
---

# GBC.Net logging

Source of truth: `docs/adr/0002-logging.md`. Do not contradict it.

## Keep the provider boundary

* `GbcNet.Core` never logs and has no logging dependency. Keep hardware hot paths free of logging.
* `GbcNet.App` depends only on `Microsoft.Extensions.Logging` abstractions.
* Inject `ILogger<TCategory>`. Use `ILoggerFactory` only when the category is selected at runtime.
* Keep Serilog packages, Serilog types, the `Serilog.Core.Logger` created by the composition root, and static `Log` calls inside the composition root (`Program`, `DependencyInjection`).
* Never inject `Serilog.ILogger` or use Serilog APIs in presenters, services, audio, input, library, saves, or other non-composition-root code.

## Define structured events

Use source-generated `[LoggerMessage]` methods.

Co-locate one uniquely named `<Owner>Log` class with its owner file and call events explicitly through that class:

```csharp
internal static partial class EmulationSessionPresenterLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Cheat codes could not be applied.")]
    internal static partial void CheatCodeApplyFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Emulation session faulted.")]
    internal static partial void EmulationFaulted(
        ILogger logger,
        Exception exception);
}

EmulationSessionPresenterLog.EmulationFaulted(logger, exception);
```

* Use stable message templates and named scalar properties. Never interpolate strings.
* Pass exceptions through an `Exception` parameter, not as a template property.
* Do not set `EventId`.
* Keep method names, categories, templates, and property names stable.
* Do not call `IsEnabled` around normal generated logging calls. Guard only expensive argument construction.
* Do not serialize EF entities, ROM contents, SRAM, save-state data, configuration objects, or other large or sensitive objects into logs.
* Use a logging scope only for context shared by several events. Do not repeat scope properties in each message.

## Choose the level by consequence

* `Warning`: an unexpected recoverable failure. The operation continues in a degraded state.
* `Error`: an operation or persistence action failed.
* `Critical`: a process-terminating failure. Serilog maps this to `Fatal`.
* `Trace` / `Debug`: targeted diagnosis only. The file sink writes `Warning` or higher.

Do not promote these to `Warning`:

* expected validation failures;
* unsupported input;
* user cancellation;
* normal lifecycle events;
* successful persistence;
* retries;
* queries;
* input events;
* frames;
* instructions;
* audio samples.

Emit at most one event for a user operation. Never log per byte, frame, tick, instruction, or sample.

## Configure Serilog in the composition root

Create one process-wide logger in `Program.Main` before configuration loading, dependency injection, and database migration:

```csharp
Log.Logger = CreateLogger(UserDataPaths.LogFilePath);
```

Do not replace it during normal startup.

Configuration:

* `MinimumLevel.Warning()`.
* Text output template:

```text
{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}
```

* Write under the per-user `logs` directory.
* Roll daily and at 2 MiB.
* Set `rollOnFileSizeLimit: true`.
* Retain at most 14 files.
* Delete rolled files older than 14 days.
* `DependencyInjection.ConfigureLogging` calls `builder.AddSerilog()` once to bridge the process logger into `Microsoft.Extensions.Logging`.
* Call `Log.CloseAndFlush()` on normal exit and at the process terminal boundary.
* Keep Avalonia `LogToTrace` debugger-only.

Do not add:

* a console sink;
* two-stage bootstrap logging;
* request logging.

## Protect sensitive data

Never add these values to repository-authored log messages, templates, scopes, or structured properties:

* ROM paths;
* user filenames;
* search text;
* configuration contents;
* SRAM;
* save-state contents;
* input bindings;
* SQL.

When useful, identify a ROM only through non-sensitive cartridge metadata, such as its title or cartridge-header checksum.

Runtime and external-library exceptions may themselves contain paths, filenames, SQL, or other sensitive details. Pass the original exception to the logger when required, but do not duplicate those values in repository-authored messages or properties.

## Background operations

Capture unexpected failures from application-owned background operations at their execution boundary.

Log where the application owns the consequence. Do not log in an intermediate layer that only enriches and rethrows the exception.
