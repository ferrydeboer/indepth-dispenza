# Architect Agent — Changelog

## v1.0.0 — 2026-05-14

Initial definition. Key design decisions:

- Compiler probing via `dotnet build` used for exhaustive dependency discovery — more reliable than LLM text-based tracing
- Four output states mirroring PO agent pattern: `design_doc`, `needs_clarification`, `needs_refactoring_first`, `returned_to_po`
- Essential vs. cosmetic refactoring distinction: essential blocks or is included in plan, cosmetic is noted only
- `filesRead` field makes analysis scope auditable at Gate 1
- Commit plan is typed (`preparatory_refactor | test | implementation`) and ordered — Developer executes sequentially
- No commit capability — Architect analyses and plans only
- `gitCommit: false` enforced in agent.yml capabilities
