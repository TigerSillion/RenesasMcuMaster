# Build/Test Rules

## Execution order
1. Run minimal target check first.
2. Run module-level validation.
3. Run full project checks only when necessary.

## Failure reporting
- Include exact command.
- Include the first relevant error and root-cause hint.
- Include deterministic repro steps.

## Mutation constraints
- Do not run formatters/linters that rewrite unrelated files.
- Do not clean or reset build folders outside task scope.
