---
name: gbc-net-logging
description: GBC.Net logging code conventions beyond ADR 0002. Use when adding or reviewing source-generated LoggerMessage methods in GbcNet.App.
---

# GBC.Net logging

Read `docs/adr/0002-logging.md` first; it is the source of truth for the logging pipeline, levels, sensitive data, exception logging boundaries, and the Core/logging separation. Do not contradict it.

The ADR does not fix the code conventions below. Apply them to every source-generated `[LoggerMessage]` method in `GbcNet.App`.

## Logger class

* Co-locate one `internal static partial class <Owner>Log` with its owner file.
* Give it a unique name derived from the owner.
* Call events explicitly through that class, never through a generic helper.

## Events

* Use stable message templates with named scalar properties. Never interpolate strings.
* Pass exceptions through an `Exception` parameter, not as a template property.
* Do not set `EventId`.
* Keep method names, categories, templates, and property names stable once emitted.

## Call sites

* Do not wrap normal generated calls in `IsEnabled`. Guard only when argument construction is expensive.
* Use a logging scope only for context shared by several events. Do not repeat scope properties inside individual messages.
