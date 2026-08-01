# Hard rules

* Use the `gameboy-development` skill before changing Game Boy hardware behavior, emulator timing, CPU, PPU, APU, timers, interrupts, DMA, MBC, CGB, or SGB logic.
* Read the applicable versioned ADR under `docs/adr/` before changing its area. ADRs are the source of truth; synchronize Codebase Memory ADR content when an ADR changes.
* Respond tersely in the user's language. Do not narrate tool calls, use decorative tables, or dump long logs unless asked.
* Preserve user changes. Do not overwrite, delete, or reformat unrelated work.

## Engineering

* Prefer modern BCL APIs and established maintained libraries over custom machinery.
* Prefer direct readable code over speculative abstractions, wrappers, helpers, fallbacks, and intermediate variables.
* Do not preserve backward compatibility unless explicitly requested.
* Make architectural decisions for the long term; do not add stopgaps intended for later replacement.

## Tests

* Agents may run `make unit`, but must not use `make tests` or `make integration`. Both execute integration tests on the host.
* Always run integration tests through Podman or Docker: `make integration-c CONFIGURATION=Release`.
* Filter a class with `make integration-c CONFIGURATION=Release TEST_ARGS="--filter-class Fully.Qualified.TestClassName"`.
* Do not use `dotnet test --filter`; this project uses Microsoft Testing Platform and does not support that option.

## Git

* Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate Git state unless the user explicitly requests that exact action.
* For Git output requests, run only the requested read-only command and report the important lines.
