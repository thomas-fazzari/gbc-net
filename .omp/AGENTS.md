# GBC.Net

## Architecture

* The only production project dependency is `GbcNet.App` → `GbcNet.Core`. Core has no project or package dependencies.
* Keep hardware state, timing, and emulation behavior in Core. Keep UI, operating-system I/O, persistence, and external adapters in App.
* Keep Core and hardware hot paths allocation-conscious and free of logging.

## Decisions

Versioned ADRs under `docs/adr/` are the source of truth. Read the applicable ADR before changing its area; do not recopy its content here.

## Workflow

Use Makefile entry points instead of duplicating their underlying commands.

* `make unit CONFIGURATION=Release`
* `make lint CONFIGURATION=Release`
* `make integration CONFIGURATION=Release`
