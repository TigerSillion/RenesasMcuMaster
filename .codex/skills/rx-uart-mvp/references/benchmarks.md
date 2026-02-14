# RX UART 2Mbps Benchmarks

## Target metrics
- Sustained payload throughput: >= 180 KB/s at 2Mbps
- Read/write command RTT: < 10 ms (small batch)
- Stream jitter: p95 frame gap within 2x nominal period

## Test matrix
1. 4 channels @ 10kHz float32 stream
2. 16 channels @ 2kHz float32 stream
3. Stream + periodic READ_MEM_BATCH mixed load

## Measurement points
- MCU: pack time, queue depth, TX stall count
- PC: read burst size, parser latency, dropped frame count

## Optimization checklist
- Increase batch payload before TX.
- Reduce per-sample headers (prefer packed frame).
- Use lock-free queue between serial thread and parser thread.
- Avoid UI thread work in serial callback.
