# Hard rules

* Do not mutate Git state unless user explicitly requests exact Git action.
* Preserve user changes. Never overwrite, delete, or reformat unrelated work.
* If rtk is available: run .NET tests from through `rtk test dotnet test` (never use
  `rtk dotnet test`)
* Use the `gameboy-development` skill before changing Game Boy hardware behavior, emulator timing, CPU, PPU, APU, timers, interrupts, DMA, MBC, CGB, or SGB logic.
* Read the applicable versioned ADR under `docs/adr/` before changing its area. ADRs are the source of truth.

## Engineering

* Make architectural decisions for the long term. Do not add stopgaps intended for later replacement.
* Prefer modern BCL and established maintained libraries over custom machinery.
* Prefer direct, readable code over speculative abstractions, wrappers, fallbacks, and compatibility layers.
* Do not preserve backward compatibility unless explicitly requested.
* Optimize measured hot paths. Design protocol I/O for bounded memory and backpressure from start.

## Writing

Apply these rules to human-facing prose stored in or submitted for the repository (e.g. Markdown, docstrings). Do not rewrite code logic, identifiers, or test assertions to satisfy them.

* Aim for a fifth-grade reading level where technical meaning allows. Use short,
  grammatical sentences and common words.
* Spell out uncommon abbreviations on first use. Common protocol and platform
  terms such as HTTP, TLS, DNS, URL, JSON, HTML, CSS, .NET, and EF Core may stay
  abbreviated when context is clear. On first use, expand BCL (`.NET base class
  library`), CQRS (`Command Query Responsibility Segregation`), OCI (`Open Container Initiative`),
  FTS (`full-text search`).
* Avoid semicolons except in dense table cells. Prefer a period.
* Cut filler lead-ins, repeated points, and facts the reader can already see.
  Question any parenthetical longer than about six words.
* Never use "hand-written" as a quality label. Name the concrete Halcyon project
  or resource instead.
