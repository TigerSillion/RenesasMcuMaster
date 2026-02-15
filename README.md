# RenesasMcuMaster (RenesasForge MVP)

Windows-first MCU debug toolchain for Renesas RX targets.

Current delivery baseline:
1. WPF/.NET desktop app (`src-dotnet/`)
2. UART transport with dual parser (`RForgeBinary` + `VOFA compatible`)
3. Variable read/write, waveform streaming, recording/export
4. UART simulator + E2E tester for repeatable regression

## Repository Entry

1. Solution entry: `RenesasForge.slnx`
2. Main app: `src-dotnet/RenesasForge.App`
3. Core/protocol/transport:
- `src-dotnet/RenesasForge.Core`
- `src-dotnet/RenesasForge.Protocol`
- `src-dotnet/RenesasForge.Transport.Serial`
4. Tools:
- UART MCU simulator: `tools/uart_mcu_sim.py`
- UART E2E test runner: `tools/uart_e2e_tester.py`

## Build And Run

```powershell
dotnet build RenesasForge.slnx -c Debug
dotnet run --project src-dotnet/RenesasForge.App
dotnet test src-dotnet/RenesasForge.Tests.Unit/RenesasForge.Tests.Unit.csproj -c Debug
```

## End-To-End UART Validation

Prerequisite: virtual serial pair is connected (example `COM8 <-> COM9`).

1. Start simulator (MCU side):
```powershell
python tools/uart_mcu_sim.py --port COM9 --baud 921600 --protocol rforge --channels 8 --stream-hz 220
```

2. Run automated protocol regression (PC side):
```powershell
python tools/uart_e2e_tester.py --port COM8 --baud 921600 --duration 6 --out build/e2e_report.json
```

3. Expected result:
- `build/e2e_report.json` contains `"ok": true`

## Documentation Index

1. MVP implementation plan: `mvp_implementation_plan.md`
2. Phase 1 plan: `Phase1_实施方案_RX_UART.md`
3. Platform plan: `mcu_platform_plan.md`
4. WPF architecture: `docs/architecture/wpf_mvp_architecture.md`
5. UART simulator usage: `docs/uart_simulator_guide.md`
6. UART protocol spec: `docs/protocol/rforge_uart_debug_protocol_v1.md`
7. Quick start checklist: `docs/quick_start.md`
8. Development log: `docs/development_log.md`
9. Qt legacy migration notes:
- `docs/migration/qt_to_wpf_gap_analysis.md`
- `docs/migration/legacy_qt_freeze_policy.md`

## Notes

1. `src/` and `CMakeLists.txt` are legacy references and not the active implementation path.
2. Active feature development is done in `src-dotnet/`.
