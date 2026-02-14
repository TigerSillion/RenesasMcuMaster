# RX UART Protocol Reference

## Frame (binary)
- SOF: 2 bytes (`0xAA 0x55`)
- Version: 1 byte
- Cmd: 1 byte
- Seq: 2 bytes
- PayloadLen: 2 bytes
- Payload: N bytes
- CRC16: 2 bytes (header+payload)

## Core command IDs
- `0x01` PING
- `0x02` STREAM_START
- `0x03` STREAM_STOP
- `0x10` GET_VAR_TABLE
- `0x11` READ_MEM_BATCH
- `0x12` WRITE_MEM
- `0x20` STREAM_DATA

## Parser requirements
- Accept split packets across multiple reads.
- Reject invalid CRC and resync by scanning SOF.
- Enforce max payload guard.
- Keep parser state machine re-entrant.

## Interop mode
- Support VOFA-compatible float stream parser in parallel path.
- Expose active parser mode in logs for diagnostics.
