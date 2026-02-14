# Development Log

## 2026-02-14

1. Migrated active implementation path to WPF/.NET (`src-dotnet/`).
2. Built main modules:
- `RenesasForge.App`
- `RenesasForge.Core`
- `RenesasForge.Protocol`
- `RenesasForge.Transport.Serial`
3. Implemented parser/transport/core functional baseline:
- RForge binary parser with CRC16-CCITT
- VOFA-compatible parser
- Serial transport abstraction and implementation
- Data/variable/record engines
4. Added unit tests for parser/core behavior.

## 2026-02-15

1. Added UART MCU simulator:
- File: `tools/uart_mcu_sim.py`
- Supports command handling, streaming, map-import, error injection.
2. Added UART simulator documentation:
- File: `docs/uart_simulator_guide.md`
3. Added automated end-to-end tester:
- File: `tools/uart_e2e_tester.py`
- Runs `PING/GET_VAR_TABLE/READ/WRITE/STREAM_START/STREAM_STOP`.
4. Fixed simulator robustness issue:
- Stream write timeout now handled without crashing stream thread.
5. Validated virtual COM flow:
- Pair confirmed on `COM8 <-> COM9`.
6. E2E result (latest):
- Report file: `build/e2e_report.json`
- Status: `ok=true`
- Stream sample run: `688 frames / 6s / 8 channels`

## Current Baseline

1. Build: pass
2. Unit test: pass
3. UART E2E: pass on configured virtual pair
4. Git backup: pushed to `git@github.com:TigerSillion/RenesasMcuMaster.git`
