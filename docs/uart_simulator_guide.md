# UART MCU Simulator Guide

## Purpose
- Simulate an MCU over UART for `RenesasForge.App`.
- Support:
1. RForge binary protocol (`PING/ACK`, `STREAM_START/STOP`, `GET_VAR_TABLE`, `READ_MEM_BATCH`, `WRITE_MEM`)
2. VOFA-compatible CSV streaming
3. Stress mode (frame drop and CRC error injection)
4. Optional variable table bootstrap from Renesas `.map`

Script:
- `tools/uart_mcu_sim.py`

## Prerequisites
- Python 3.10+
- `pyserial` (`pip install pyserial`)
- Windows virtual COM pair tool (recommended `com0com`) to bridge GUI and simulator.

## Typical Setup (Windows)
1. Create a virtual COM pair (example): `COM8 <-> COM9`.
2. Open GUI on one side (`COM8`).
3. Run simulator on the other side (`COM9`).

## Run Examples
1. RForge baseline:
```powershell
python tools/uart_mcu_sim.py --port COM9 --baud 921600 --protocol rforge --channels 8 --stream-hz 200 --auto-stream
```

2. RForge + map-derived variable table:
```powershell
python tools/uart_mcu_sim.py `
  --port COM9 `
  --baud 921600 `
  --protocol rforge `
  --auto-stream `
  --map-file "c:\A_sw_workspace\e2s_workspace\e2workspace_202510\RX26T_RMW_ICS_MTU_GPT_Sensorless_IHM16M1_26TF_64PIN_ADCOFFSET-main\HardwareDebug\RX26T_RMW_ICS_MTU_GPT_Sensorless_IHM16M1.map"
```

3. Stress mode (drop + CRC error):
```powershell
python tools/uart_mcu_sim.py `
  --port COM9 `
  --baud 921600 `
  --protocol rforge `
  --channels 16 `
  --stream-hz 400 `
  --auto-stream `
  --drop-rate 0.02 `
  --crc-error-rate 0.01
```

4. VOFA mode:
```powershell
python tools/uart_mcu_sim.py --port COM9 --baud 921600 --protocol vofa --channels 8 --stream-hz 150 --auto-stream
```

## Notes About MAP Integration
- The simulator reads global `data ,g` symbols and filters by name prefix.
- Default prefixes:
1. `g_`
2. `com_`
3. `gui_`
- Default minimum address filter: `0x1000` (override by `--map-min-addr`).
- Override with `--map-prefix`, example:
```powershell
--map-prefix "g_,motor_,ctrl_"
```

## Current Protocol Mapping in Simulator
1. `PING` -> `ACK`
2. `STREAM_START` -> start stream + `ACK`
3. `STREAM_STOP` -> stop stream + `ACK`
4. `GET_VAR_TABLE` -> response with text or binary var table
5. `READ_MEM_BATCH` -> response with text or binary values
6. `WRITE_MEM` -> updates local variable value + `ACK`

## Recommended GUI Validation Flow
1. Connect serial.
2. Click `PING`.
3. Click `GET VAR TABLE`.
4. Select variable -> `Read Sel`.
5. `STREAM START` and observe waveform + stats update.
6. Run stress mode and verify:
- no crash
- recoverable parser errors
- variable read/write still functional
