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
        # Keep scanning for SOF to recover from any invalid bytes in stream.
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


def decode_var_table_text(payload: bytes):
    text = payload.decode("ascii", errors="ignore").strip()
    if not text:
        return []
    items = []
    for seg in text.split(";"):
        p = [x.strip() for x in seg.split(",")]
        if len(p) >= 5:
            items.append({"name": p[0], "address": p[1], "type": p[2], "scale": p[3], "unit": p[4]})
    return items


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
        "ok": False,
    }

    with serial.Serial(args.port, args.baud, timeout=0.02) as ser:
        # 1) Connectivity handshake.
        ser.write(build_frame(0x01, seq, b""))
        seq += 1
        f = wait_frame(ser, rx, 0x02, 1.5)
        report["steps"].append({"name": "PING->ACK", "ok": f is not None})

        # 2) Variable table fetch.
        ser.write(build_frame(0x10, seq, b""))
        seq += 1
        f = wait_frame(ser, rx, 0x10, 2.0)
        vars_ = decode_var_table_text(f[2]) if f else []
        report["steps"].append({"name": "GET_VAR_TABLE", "ok": f is not None and len(vars_) > 0, "count": len(vars_)})

        # 3) Read-back first mapped variable.
        if vars_:
            addr = int(vars_[0]["address"], 16)
            payload = struct.pack("<IH", addr, 4)
            ser.write(build_frame(0x11, seq, payload))
            seq += 1
            f = wait_frame(ser, rx, 0x11, 2.0)
            report["steps"].append({"name": "READ_MEM_BATCH", "ok": f is not None and len(f[2]) > 0})
        else:
            report["steps"].append({"name": "READ_MEM_BATCH", "ok": False, "reason": "no vars"})

        # 4) Write first mapped variable.
        if vars_:
            addr = int(vars_[0]["address"], 16)
            payload = struct.pack("<If", addr, 42.5)
            ser.write(build_frame(0x12, seq, payload))
            seq += 1
            f = wait_frame(ser, rx, 0x02, 1.5)
            report["steps"].append({"name": "WRITE_MEM->ACK", "ok": f is not None})
        else:
            report["steps"].append({"name": "WRITE_MEM->ACK", "ok": False, "reason": "no vars"})

        # 5) Start streaming and wait for command ACK.
        ser.write(build_frame(0x03, seq, b""))
        seq += 1
        ack = wait_frame(ser, rx, 0x02, 1.5)
        report["steps"].append({"name": "STREAM_START->ACK", "ok": ack is not None})

        # 6) Capture stream frames for a fixed window.
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

        # 7) Stop stream and ensure control channel is still responsive.
        ser.write(build_frame(0x04, seq, b""))
        seq += 1
        ack = wait_frame(ser, rx, 0x02, 1.5)
        report["steps"].append({"name": "STREAM_STOP->ACK", "ok": ack is not None})

    report["ok"] = all(step.get("ok", False) for step in report["steps"])
    out_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0 if report["ok"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
