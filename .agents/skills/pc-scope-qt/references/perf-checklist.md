# Performance Checklist

## FPS and latency
- 16 channels active: >= 60 FPS target.
- Zoom/pan interaction < 50 ms visible response.
- Command-to-visual update latency measured and logged.

## CPU and memory
- CPU stable under continuous stream (record median and p95).
- No unbounded queue growth.
- Memory growth bounded in 8-hour run.

## Data correctness
- No silent channel reordering.
- Timestamp monotonicity maintained.
- Recorder/replay consistency spot-checked.
