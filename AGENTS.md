# RenesasMcuMaster AGENTS

## Scope
- This file defines project-level behavior only.
- Environment/runtime knobs live in `.codex/config.toml`.

## Workflow
- Follow: Explore -> Plan -> Implement -> Validate -> Report.
- Prefer small, verifiable increments.

## Command Safety
- Never run destructive commands without explicit user approval.
- Forbidden unless explicitly requested: `git reset --hard`, `git checkout --`, recursive delete outside clear scope.
- Prefer `rg` for search; if unavailable in PowerShell, fall back to `Select-String`.

## Change Policy
- Do not revert or overwrite user-owned edits unless asked.
- Prefer `apply_patch` for focused single-file edits.
- Keep changes minimal and directly tied to the task.

## Build and Test
- Run the smallest relevant validation first, then expand coverage.
- If tests fail, report exact command, key log lines, and reproduction steps.

## Review Standard
- For review requests, default to defects-first output.
- Order findings by severity: S0, S1, S2.
- Each finding must include file path and line number.

## Output Style
- Start with conclusion, then evidence (files/commands/tests).
- Use concise, actionable language.

## OpenAI Docs MCP Usage
- For OpenAI API/Codex/ChatGPT Apps SDK questions, use `openaiDeveloperDocs` MCP first.
- Do not invent undocumented behavior when docs can be queried.
