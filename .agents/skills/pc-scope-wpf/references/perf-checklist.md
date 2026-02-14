# Performance Checklist (WPF)

## FPS and latency
- 16 channels active: >= 60 FPS target.
- Zoom/pan interaction < 50 ms visible response.

## CPU and memory
- No unbounded queue growth.
- Memory growth bounded in 8-hour run.

## Data correctness
- No silent channel reorder.
- Timestamp monotonicity maintained.
