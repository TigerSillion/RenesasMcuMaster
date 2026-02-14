# MVVM Patterns for RenesasForge WPF

## Boundaries
- Transport: port lifecycle and raw bytes only.
- Protocol: bytes to typed frames.
- Core Engine: buffering, timestamps, variable model.
- ViewModel: UI-ready projections and commands.
- XAML Views: presentation only.

## Threading rules
- Never parse bytes on UI thread.
- Never block SerialPort event callback with heavy compute.
- Use Dispatcher only for state projection.
