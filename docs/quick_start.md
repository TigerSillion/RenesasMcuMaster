# Quick Start (WPF + UART)

## 1. Environment

1. Windows + .NET SDK 10.x
2. Python 3.10+
3. `pyserial` installed:
```powershell
python -m pip install pyserial
```
4. Virtual serial pair ready (recommended): `COM8 <-> COM9`

## 2. Build

```powershell
dotnet build RenesasForge.slnx -c Debug
```

## 3. Start UART Simulator (MCU side)

```powershell
python tools/uart_mcu_sim.py --port COM9 --baud 921600 --protocol rforge --channels 8 --stream-hz 220
```

Optional MAP-import mode:
```powershell
python tools/uart_mcu_sim.py `
  --port COM9 `
  --baud 921600 `
  --protocol rforge `
  --map-file "c:\path\to\project.map"
```

## 4. Start GUI (PC side)

```powershell
dotnet run --project src-dotnet/RenesasForge.App
```

Inside GUI:
1. Select `COM8`
2. Click `Connect`
3. Click `PING`, `GET VAR TABLE`, `STREAM START`

## 5. Automated E2E Regression

```powershell
python tools/uart_e2e_tester.py --port COM8 --baud 921600 --duration 6 --out build/e2e_report.json
```

Pass criteria:
1. `build/e2e_report.json` exists
2. `ok=true`
3. `stream_frames > 20`

## 6. Troubleshooting

1. `COM8/COM9` visible but no data:
- Ensure VSPE device is a `Pair`, not two isolated `Connector` devices.
2. `PING` timeout under high stream load:
- Disable auto-stream during command handshake, then issue `STREAM_START`.
3. Write timeout in simulator:
- Lower `--stream-hz` or reduce `--channels`.
