---
name: codebase-memory
description: Use Codebase Memory before broad repository exploration, architecture analysis, symbol tracing, impact analysis, or large grep/read sequences.
alwaysApply: true
---

# Codebase Memory workflow

Use the `codebase-memory` MCP graph as the first repository-discovery layer.

## What Codebase Memory is

Codebase Memory is a local structural index of the repository exposed through MCP. It parses the code into a graph of files, symbols and relationships such as definitions, calls, imports and inheritance, then provides tools for architecture summaries, symbol search, call tracing, code snippets and Git change-impact analysis.

It is not conversational memory and does not replace the source code. The index is a fast discovery layer: use it to narrow the investigation, then verify important conclusions in the actual files. Repository data stays local; no SaaS or external model is involved.

## Before broad exploration

1. Check whether the current repository is indexed with `list_projects` and `index_status`.
2. If it is missing or stale, run `index_repository` with the absolute repository root.
3. Use `get_architecture` before reading many files manually.

## Finding code

- Use `search_graph` to locate classes, functions, methods, interfaces, routes, modules and files.
- Use `get_code_snippet` only after obtaining the exact qualified name from `search_graph`.
- Use `search_code` for literal text searches inside indexed files.
- Fall back to grep/glob only when the graph cannot answer the question or when exact unindexed text is required.

## Understanding relationships

- Use `trace_path` for inbound and outbound call chains.
- Use `detect_changes` to evaluate the blast radius of current Git changes before implementation or review.
- Use `get_graph_schema` before writing a custom `query_graph` query.
- Use `query_graph` for relationship questions not covered by the higher-level tools.

## Architecture decisions

- Read existing ADR data when architecture history is relevant.
- Do not create, update or delete an ADR through `manage_adr` unless the user explicitly requests it or the repository already defines that workflow.

## Reliability

- Treat the graph as an index, not as the final source of truth.
- Before editing code, verify important conclusions against the actual source files.
- If a query returns no result, first verify the project name and exact symbol with `list_projects` and `search_graph`.
- Re-index after structural changes when the watcher has not refreshed the graph.
