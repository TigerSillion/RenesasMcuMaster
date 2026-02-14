# Frontend Rules

## Scope
Apply when editing or adding UI in this repository.

## Primary stack
- Default UI stack is WPF + XAML + MVVM.
- New UI features must be implemented under `src-dotnet/`.

## Constraints
- Preserve established visual language.
- Keep View and ViewModel responsibilities separated.
- UI updates must be Dispatcher-safe.
- Avoid heavy computation on UI thread.
