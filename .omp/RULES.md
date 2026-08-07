# Hard rules

* Do not mutate Git state unless user explicitly requests exact Git action.
* Preserve user changes. Never overwrite, delete, or reformat unrelated work.
* If RTK is available: agents must not execute Make targets. They should read the Makefile as the canonical
  reference, then run the underlying commands directly through the matching RTK
  wrappers so output remains filtered.
* Use the `gameboy-development` skill before changing Game Boy hardware behavior, emulator timing, CPU, PPU, APU, timers, interrupts, DMA, MBC, CGB, or SGB logic.
* Read the applicable versioned ADR under `docs/adr/` before changing its area. ADRs are the source of truth.

## Communication

* Reply in user's dominant language. Stay ultra-terse but technically complete.
* Drop filler, pleasantries, hedging, repetition, decorative tables. State each
  fact once. Fragments are fine.
* Prefer short clear words. Omit articles or conjunctions only when meaning stays
  unambiguous. Never invent prose abbreviations or use arrows as shorthand.
* Keep technical terms, API names, code, commands, commit text, and exact errors unchanged.
* Quote only decisive error lines unless full output is requested.
* Use explicit full language for security warnings, irreversible actions, ordered operations, ambiguity, or clarification. Resume terse style afterward.
* Never announce this style. Disable it only when user says `stop caveman` or `normal mode`.

## Writing

Apply these rules to human-facing prose stored in or submitted for the repository (e.g. Markdown, documentation comments and docstrings). Do not rewrite code logic, identifiers, or test assertions to
satisfy them.

* Aim for a fifth-grade reading level where technical meaning allows. Use short,
  grammatical sentences and common words.
* Spell out uncommon abbreviations on first use. Common hardware and platform
  terms such as CPU, PPU, APU, DMA, VRAM, OAM, ROM, RAM, may
  stay abbreviated when context is clear. On first use, potentially expand these terms, for example: DMG (original
  Game Boy), CGB (Game Boy Color), SGB (Super Game Boy), MBC (Memory Bank
  Controller), HLE (high-level emulation).
* Avoid semicolons except in dense table cells. Prefer a period.
* Cut filler lead-ins, repeated points, and facts the reader can already see.
  Question any parenthetical longer than about six words.
* Never use "hand-written" as a quality label. Name the concrete GBC.Net
  project or resource instead.

## Engineering

* Make architectural decisions for the long term; do not add stopgaps intended for later replacement.
* Prefer modern BCL and established maintained libraries over custom machinery.
* Prefer direct, readable code over speculative abstractions, wrappers, fallbacks, and compatibility layers.
* Do not preserve backward compatibility unless explicitly requested.
* Optimize measured hot paths; design protocol I/O for bounded memory and backpressure from start.

## Tests

* Agents using RTK must run .NET tests from `emulator/` through `rtk test dotnet test`; never use `rtk dotnet test`.
* Agents may run `make unit`, but must not use `make tests` or `make integration`. Both execute integration tests on the host.
* Always run integration tests through Podman or Docker: `make integration-c CONFIGURATION=Release`.
* Filter a class with `make integration-c CONFIGURATION=Release TEST_ARGS="--filter-class Fully.Qualified.TestClassName"`.
* Do not use `dotnet test --filter`; this project uses Microsoft Testing Platform and does not support that option.
