# Safety Rules

## Purpose
Define high-risk command policy and escalation constraints.

## Blacklist (forbidden unless user explicitly requests)
- `git reset --hard`
- `git checkout -- <path>`
- `rm -rf` / `Remove-Item -Recurse -Force` on broad paths

## Prompt-required patterns
- Privileged package installs
- Shell commands that combine write + network operations
- Destructive cleanup commands

## Escalation requirements
- Every escalated command must include a clear `justification`.
- Prefix approvals should be narrow and task-scoped.
