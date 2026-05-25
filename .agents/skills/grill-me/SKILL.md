---
name: grill-me
description: Clarify or stress-test non-trivial features, refactors, architecture changes, or plans before editing. Use when the user says grill me, asks for design review, wants blind spots surfaced, or AGENTS.md requires clarification before non-trivial work.
---

# Grill Me

Use this before work that changes more than one module, changes public/persisted contracts,
adds significant logic, moves ownership between modules, or changes architecture. Skip only
when the user explicitly says to skip grill-me.

Goal: find the most critical missing decision required for correct implementation.

## Workflow

1. Inspect code first when the answer is discoverable locally.
2. Identify the decision that most affects file ownership, module boundaries, public contracts, data flow, or design pattern.
3. Ask one focused question.
4. Include a recommended answer.
5. After user answers, either implement or ask the next blocking question.

## Question Format

Use this shape:

```text
Question: <one concrete decision>
Reco: <default answer and why>
Impact: <what changes depending on answer>
```

## Decision Table

Evaluate top to bottom. First matching row wins.

| Situation | Action |
| --- | --- |
| User explicitly says `skip grill-me` | Do not ask; execute. |
| Request is unrelated to planning/design or input is irrelevant | Ask one intent-clarifying question. |
| Requirements are incomplete or contradictory | Ask one blocking clarification question. |
| Task is config tweak, rename, comment/doc tweak, formatting, or single-file trivial edit | Do not ask; execute. |
| Codebase shows exactly one existing pattern and no contract/ownership change | Do not ask; execute. |
| Task changes more than one module, public/persisted contracts, data flow, ownership, or architecture | Ask one question with recommendation. |
| User asks for design review, blind spots, stress-test, or `grill me` | Ask one question with recommendation. |
| None of the above | Do not ask; execute. |

## Standards

- Be direct, not performative.
- Do not ask a list of questions at once.
- Do not block on preferences that can be inferred from code.
- Do not use this skill to avoid doing work.
- Once direction is clear, stop grilling and execute.
