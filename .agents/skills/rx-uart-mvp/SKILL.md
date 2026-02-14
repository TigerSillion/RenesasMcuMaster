---
name: rx-uart-mvp
description: Use when implementing or optimizing Renesas RX UART telemetry MVP, including protocol framing, batch variable read/write, throughput tuning at up to 2Mbps, and PC-MCU interoperability diagnostics.
---

# RX UART MVP Skill

## When to use
- RX MCU UART protocol design/implementation.
- Variable table read/write and stream frame integration.
- Throughput and latency tuning for 2Mbps-class serial links.

## Do not use
- UI rendering tasks unrelated to transport/protocol.
- USB CDC/SWD/RTT implementation details (future phase unless explicitly requested).

## Workflow
1. Confirm transport assumptions: baud, frame size, timer source, error recovery mode.
2. Apply the frame contract in `references/protocol.md`.
3. Validate parser resilience: partial frame, CRC fail, resync behavior.
4. Run throughput checklist from `references/benchmarks.md`.
5. Report bottleneck location (MCU encode, UART TX, PC parse, UI queue).

## Required outputs
- Protocol compatibility statement (self protocol + VOFA-compatible path).
- Measured throughput/latency summary with test conditions.
- Concrete next optimization step.

## Reference files
- `references/protocol.md`
- `references/benchmarks.md`
