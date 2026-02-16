#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import struct
import time
from pathlib import Path

import serial


def crc16_ccitt(data: bytes, init: int = 0xFFFF) -> int:
    crc = init
    for b in data:
        crc ^= b << 8
        for _ in range(8):
            if crc & 0x8000:
                crc = ((crc << 1) ^ 0x1021) & 0xFFFF
            else:
                crc = (crc << 1) & 0xFFFF
    return crc


def build_frame(cmd: int, seq: int, payload: bytes) -> bytes:
    hdr = b"\xAA\x55" + bytes([1, cmd]) + struct.pack("<H", seq) + struct.pack("<H", len(payload))
    crc = crc16_ccitt(hdr[2:] + payload)
    return hdr + payload + struct.pack("<H", crc)


def parse_frames(buf: bytearray):
    out = []
    while True:
        if len(buf) < 10:
            break
        sof = buf.find(b"\xAA\x55")
        if sof < 0:
            buf.clear()
            break
        if sof > 0:
            del buf[:sof]
            if len(buf) < 10:
                break
        n = struct.unpack_from("<H", buf, 6)[0]
        if n > 1024:
            del buf[0]
            continue
        total = 8 + n + 2
        if len(buf) < total:
            break
        got = struct.unpack_from("<H", buf, 8 + n)[0]
        exp = crc16_ccitt(bytes(buf[2 : 8 + n]))
        if got != exp:
            del buf[0]
            continue
        cmd = buf[3]
        seq = struct.unpack_from("<H", buf, 4)[0]
        payload = bytes(buf[8 : 8 + n])
        del buf[:total]
        out.append((cmd, seq, payload))
    return out


def wait_frame(ser: serial.Serial, rx: bytearray, cmd: int, timeout_s: float):
    deadline = time.time() + timeout_s
    while time.time() < deadline:
        data = ser.read(4096)
        if data:
            rx.extend(data)
            for c, s, p in parse_frames(rx):
                if c == cmd:
                    return c, s, p
        time.sleep(0.005)
    return None


def parse_ack(payload: bytes):
    if len(payload) < 4:
        return None
    status = payload[0]
    for_cmd = payload[1]
    for_seq = struct.unpack_from("<H", payload, 2)[0]
    return {"status": status, "for_cmd": for_cmd, "for_seq": for_seq}


def ack_ok(frame, expected_cmd: int) -> tuple[bool, dict]:
    detail = {"legacy": False}
    if frame is None:
        return False, detail
    ack = parse_ack(frame[2])
    if ack is None:
        detail["legacy"] = True
        return True, detail
    detail.update(ack)
    return ack["status"] == 0 and ack["for_cmd"] == expected_cmd, detail


def decode_var_table_text(payload: bytes):
    text = payload.decode("ascii", errors="ignore").strip()
    if not text:
        return []
    items = []
    for seg in text.split(";"):
        p = [x.strip() for x in seg.split(",")]
        if len(p) >= 5:
            items.append(
                {
                    "name": p[0],
                    "address": p[1],
                    "type": p[2],
                    "scale": p[3],
                    "unit": p[4],
                }
            )
    return items


def decode_var_table_binary(payload: bytes):
    if len(payload) < 2:
        return []
    idx = 0
    count = struct.unpack_from("<H", payload, idx)[0]
    idx += 2
    items = []
    for _ in range(count):
        if idx + 13 > len(payload):
            return []
        address = struct.unpack_from("<I", payload, idx)[0]
        idx += 4
        dtype = payload[idx]
        idx += 1
        array_size = struct.unpack_from("<H", payload, idx)[0]
        idx += 2
        scale = struct.unpack_from("<f", payload, idx)[0]
        idx += 4
        unit_len = payload[idx]
        idx += 1
        name_len = payload[idx]
        idx += 1
        if idx + unit_len + name_len > len(payload):
            return []
        unit = payload[idx : idx + unit_len].decode("ascii", errors="ignore")
        idx += unit_len
        name = payload[idx : idx + name_len].decode("ascii", errors="ignore")
        idx += name_len
        items.append(
            {
                "name": name,
                "address": f"0x{address:08X}",
                "type": str(dtype),
                "scale": f"{scale:.6g}",
                "unit": unit,
                "array_size": array_size,
            }
        )
    return items


def decode_var_table(payload: bytes):
    vars_binary = decode_var_table_binary(payload)
    if vars_binary:
        return vars_binary, "binary"
    vars_text = decode_var_table_text(payload)
    if vars_text:
        return vars_text, "text"
    return [], "unknown"


def decode_readmem_binary(payload: bytes):
    if len(payload) < 2:
        return {}
    idx = 0
    count = struct.unpack_from("<H", payload, idx)[0]
    idx += 2
    values = {}
    for _ in range(count):
        if idx + 6 > len(payload):
            return {}
        address = struct.unpack_from("<I", payload, idx)[0]
        idx += 4
        size = struct.unpack_from("<H", payload, idx)[0]
        idx += 2
        if idx + size > len(payload):
            return {}
        values[address] = bytes(payload[idx : idx + size])
        idx += size
    return values


def decode_readmem_text(payload: bytes):
    text = payload.decode("ascii", errors="ignore").strip()
    if not text:
        return {}
    values = {}
    for seg in text.split(","):
        pair = [x.strip() for x in seg.split("=")]
        if len(pair) != 2:
            continue
        try:
            addr_text = pair[0]
            if addr_text.lower().startswith("0x"):
                address = int(addr_text[2:], 16)
            else:
                address = int(addr_text)
            value = float(pair[1])
        except ValueError:
            continue
        values[address] = struct.pack("<f", value)
    return values


def decode_readmem(payload: bytes):
    binary = decode_readmem_binary(payload)
    if binary:
        return binary, "binary"
    text = decode_readmem_text(payload)
    if text:
        return text, "text"
    return {}, "unknown"


def decode_numeric(raw: bytes):
    if len(raw) >= 8:
        return struct.unpack_from("<d", raw, 0)[0]
    if len(raw) >= 4:
        return struct.unpack_from("<f", raw, 0)[0]
    if len(raw) >= 2:
        return float(struct.unpack_from("<h", raw, 0)[0])
    if len(raw) >= 1:
        return float(struct.unpack_from("<b", raw, 0)[0])
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", default="COM8")
    ap.add_argument("--baud", type=int, default=921600)
    ap.add_argument("--duration", type=float, default=6.0, help="stream capture duration seconds")
    ap.add_argument("--out", default="build/e2e_report.json")
    args = ap.parse_args()

    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    seq = 1
    rx = bytearray()
    report = {
        "port": args.port,
        "baud": args.baud,
        "steps": [],
        "stream_frames": 0,
        "stream_channels_last": 0,
        "var_table_format": "unknown",
        "readmem_format": "unknown",
        "ok": False,
    }

    with serial.Serial(args.port, args.baud, timeout=0.02) as ser:
        # 1) Connectivity handshake.
        ser.write(build_frame(0x01, seq, b""))
        ping_seq = seq
        seq += 1
        f = wait_frame(ser, rx, 0x02, 1.5)
        ok, detail = ack_ok(f, 0x01)
        detail["tx_seq"] = ping_seq
        report["steps"].append({"name": "PING->ACK", "ok": ok, "detail": detail})

        # 2) Stream configuration.
        set_stream_payload = struct.pack("<BBHH", 8, 0, 220, 0)
        ser.write(build_frame(0x05, seq, set_stream_payload))
        cfg_seq = seq
        seq += 1
        f = wait_frame(ser, rx, 0x02, 1.5)
        ok, detail = ack_ok(f, 0x05)
        detail["tx_seq"] = cfg_seq
        report["steps"].append({"name": "SET_STREAM_CONFIG->ACK", "ok": ok, "detail": detail})

        # 3) Variable table fetch.
        ser.write(build_frame(0x10, seq, b""))
        seq += 1
        f = wait_frame(ser, rx, 0x10, 2.0)
        vars_, var_format = decode_var_table(f[2]) if f else ([], "unknown")
        report["var_table_format"] = var_format
        report["steps"].append({"name": "GET_VAR_TABLE", "ok": f is not None and len(vars_) > 0, "count": len(vars_), "format": var_format})

        # 4) Read first mapped variable.
        first_addr = None
        if vars_:
            first_addr = int(vars_[0]["address"], 16)
            payload = struct.pack("<IH", first_addr, 4)
            ser.write(build_frame(0x11, seq, payload))
            seq += 1
            f = wait_frame(ser, rx, 0x11, 2.0)
            values, read_format = decode_readmem(f[2]) if f else ({}, "unknown")
            report["readmem_format"] = read_format
            report["steps"].append(
                {
                    "name": "READ_MEM_BATCH",
                    "ok": f is not None and first_addr in values,
                    "format": read_format,
                    "count": len(values),
                }
            )
        else:
            report["steps"].append({"name": "READ_MEM_BATCH", "ok": False, "reason": "no vars"})

        # 5) Write and verify first mapped variable.
        if first_addr is not None:
            target = 42.5
            write_payload = struct.pack("<IHf", first_addr, 4, target)
            ser.write(build_frame(0x12, seq, write_payload))
            write_seq = seq
            seq += 1
            f = wait_frame(ser, rx, 0x02, 1.5)
            ok, detail = ack_ok(f, 0x12)
            detail["tx_seq"] = write_seq
            report["steps"].append({"name": "WRITE_MEM->ACK", "ok": ok, "detail": detail})

            verify_payload = struct.pack("<IH", first_addr, 4)
            ser.write(build_frame(0x11, seq, verify_payload))
            seq += 1
            f = wait_frame(ser, rx, 0x11, 2.0)
            values, read_format = decode_readmem(f[2]) if f else ({}, "unknown")
            raw = values.get(first_addr, b"")
            numeric = decode_numeric(raw) if raw else None
            verify_ok = numeric is not None and abs(numeric - target) < 0.6
            report["steps"].append(
                {
                    "name": "WRITE_VERIFY",
                    "ok": verify_ok,
                    "value": numeric,
                    "target": target,
                    "format": read_format,
                }
            )
        else:
            report["steps"].append({"name": "WRITE_MEM->ACK", "ok": False, "reason": "no vars"})
            report["steps"].append({"name": "WRITE_VERIFY", "ok": False, "reason": "no vars"})

        # 6) Start streaming.
        ser.write(build_frame(0x03, seq, b""))
        stream_start_seq = seq
        seq += 1
        ack = wait_frame(ser, rx, 0x02, 1.5)
        ok, detail = ack_ok(ack, 0x03)
        detail["tx_seq"] = stream_start_seq
        report["steps"].append({"name": "STREAM_START->ACK", "ok": ok, "detail": detail})

        # 7) Capture stream frames for a fixed window.
        t_end = time.time() + args.duration
        stream_frames = 0
        last_channels = 0
        while time.time() < t_end:
            data = ser.read(4096)
            if data:
                rx.extend(data)
                for c, _s, p in parse_frames(rx):
                    if c == 0x20 and len(p) >= 8 and (len(p) - 8) % 6 == 0:
                        stream_frames += 1
                        last_channels = (len(p) - 8) // 6
            time.sleep(0.002)
        report["stream_frames"] = stream_frames
        report["stream_channels_last"] = last_channels
        report["steps"].append({"name": "STREAM_DATA", "ok": stream_frames > 20, "frames": stream_frames, "channels": last_channels})

        # 8) Stop stream and ensure control channel is still responsive.
        ser.write(build_frame(0x04, seq, b""))
        stream_stop_seq = seq
        seq += 1
        ack = wait_frame(ser, rx, 0x02, 1.5)
        ok, detail = ack_ok(ack, 0x04)
        detail["tx_seq"] = stream_stop_seq
        report["steps"].append({"name": "STREAM_STOP->ACK", "ok": ok, "detail": detail})

    report["ok"] = all(step.get("ok", False) for step in report["steps"])
    out_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0 if report["ok"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
