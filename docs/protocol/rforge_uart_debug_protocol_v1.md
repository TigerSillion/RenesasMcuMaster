# RForge UART Debug Protocol v1 (RX MCU)

## 1. Scope
1. This spec is for RX MCU debug over UART between MCU firmware and `RenesasForge.App`.
2. It keeps existing waveform and variable algorithms unchanged, and only defines transport framing and command payloads.

## 2. Transport Frame
1. Byte order: little-endian.
2. Frame layout:
   `SOF(2) + ver(1) + cmd(1) + seq(2) + len(2) + payload(len) + crc16(2)`
3. Constants:
   - `SOF = 0xAA 0x55`
   - `ver = 0x01`
   - `len <= 1024`
   - `crc16 = CRC16-CCITT(poly=0x1021, init=0xFFFF)` over `ver..payload`.

## 3. Command Set
1. `0x01 PING`
2. `0x02 ACK`
3. `0x03 STREAM_START`
4. `0x04 STREAM_STOP`
5. `0x05 SET_STREAM_CONFIG`
6. `0x10 GET_VAR_TABLE`
7. `0x11 READ_MEM_BATCH`
8. `0x12 WRITE_MEM`
9. `0x20 STREAM_DATA`

## 4. Payload Definitions

### 4.1 `ACK (0x02)`
1. Recommended payload (4 bytes):
   - `status:u8` (`0=OK,1=INVALID_CMD,2=INVALID_PAYLOAD,3=BUSY,4=DENIED`)
   - `for_cmd:u8` (command being acknowledged)
   - `for_seq:u16` (sequence of command being acknowledged)
2. Backward compatibility: zero-length ACK is still accepted by PC.

### 4.2 `SET_STREAM_CONFIG (0x05)` request
1. Payload (6 bytes):
   - `channel_count:u8`
   - `reserved:u8` (set 0)
   - `stream_hz:u16`
   - `flags:u16` (bit0=include_ts, other bits reserved)
2. MCU should ACK with status and apply accepted fields.

### 4.3 `GET_VAR_TABLE (0x10)` response
1. Binary format (recommended):
   - `count:u16`
   - repeated `VarDesc`:
     - `addr:u32`
     - `type:u8` (`0..7` for int8/uint8/int16/uint16/int32/uint32/float32/float64)
     - `array_size:u16`
     - `scale:f32`
     - `unit_len:u8`
     - `name_len:u8`
     - `unit[unit_len]`
     - `name[name_len]`
2. Legacy text format is still tolerated by PC.

### 4.4 `READ_MEM_BATCH (0x11)` request/response
1. Request payload:
   - repeated `ReadReq`: `addr:u32 + size:u16`
2. Response payload (binary recommended):
   - `count:u16`
   - repeated `ReadItem`:
     - `addr:u32`
     - `size:u16`
     - `raw[size]`

### 4.5 `WRITE_MEM (0x12)` request
1. New v1 payload (recommended):
   - repeated `WriteItem`:
     - `addr:u32`
     - `size:u16`
     - `raw[size]`
2. MCU should infer decode based on variable type table and ACK result.
3. Legacy mode `[addr:u32 + value:f32]` may be kept for compatibility tools.

### 4.6 `STREAM_DATA (0x20)` response
1. Binary stream payload:
   - `ts_us:u64`
   - repeated sample: `channel_id:u16 + value:f32`
2. Optional VOFA text stream remains supported for compatibility.

## 5. Error Recovery
1. On CRC fail or invalid length, receiver drops one byte and re-scans for next SOF.
2. Keep parser state machine non-blocking; never wait forever for partial frame.

## 6. Throughput Notes For RX
1. For high baud (921600~2Mbps), prefer DMA/ring-buffer UART RX.
2. Keep per-frame payload compact; avoid many tiny ACK-only loops during streaming.
3. If CPU is saturated, return `ACK status=BUSY` instead of silent drop.
