---
name: pc-scope-wpf
description: Use when building or refactoring the PC WPF/.NET oscilloscope app architecture, including MVVM boundaries, Dispatcher-safe data flow, transport-parser-engine pipeline, and performance validation.
---

# PC Scope WPF Skill

## When to use
- WPF/XAML architecture and module boundaries.
- MVVM data flow from transport -> parser -> engine -> UI.
- Threading and Dispatcher safety.

## Workflow
1. Lock module boundaries and ownership.
2. Validate transport -> parser -> data engine pipeline.
3. Ensure UI updates are Dispatcher-safe and lightweight.
4. Run checklist in `references/perf-checklist.md`.
5. Document regressions and fallback behavior.

## Required outputs
- Updated boundary map and key interfaces.
- Thread handoff description.
- Performance delta under representative load.

## Reference files
- `references/mvvm-patterns.md`
- `references/perf-checklist.md`
