#!/usr/bin/env python3
"""
RenesasForge UART MCU simulator.

Features:
- RForge binary protocol command handling
- VOFA-style CSV streaming
- Configurable baud, stream rate, channel count, CRC/drop error injection
- Optional variable table bootstrap from Renesas .map files
"""

from __future__ import annotations

import argparse
import math
import random
import re
import struct
import threading
import time
from dataclasses import dataclass
from enum import IntEnum
from pathlib import Path
from typing import Dict, List, Sequence

import serial


class CommandId(IntEnum):
    Ping = 0x01
    Ack = 0x02
    StreamStart = 0x03
    StreamStop = 0x04
    SetStreamConfig = 0x05
    GetVarTable = 0x10
    ReadMemBatch = 0x11
    WriteMem = 0x12
    StreamData = 0x20


class DataType(IntEnum):
    Int8 = 0
    UInt8 = 1
    Int16 = 2
    UInt16 = 3
    Int32 = 4
    UInt32 = 5
    Float32 = 6
    Float64 = 7


@dataclass
class Variable:
    name: str
    address: int
    dtype: DataType
    scale: float = 1.0
    unit: str = ""
    value: float = 0.0


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


def build_rforge_frame(cmd: int, seq: int, payload: bytes, crc_error_rate: float = 0.0) -> bytes:
    if len(payload) > 1024:
        payload = payload[:1024]
    header = b"\xAA\x55" + bytes([1, cmd]) + struct.pack("<H", seq) + struct.pack("<H", len(payload))
    core = header[2:] + payload
    crc = crc16_ccitt(core)
    frame = bytearray(header + payload + struct.pack("<H", crc))
    if crc_error_rate > 0 and random.random() < crc_error_rate and len(frame) > 10:
        idx = random.randint(8, len(frame) - 3)
        frame[idx] ^= 0x01
    return bytes(frame)


def iter_rforge_frames(buffer: bytearray):
    while True:
        if len(buffer) < 10:
            return
        # Resync on SOF to tolerate line noise / framing errors.
        sof = buffer.find(b"\xAA\x55")
        if sof < 0:
            buffer.clear()
            return
        if sof > 0:
            del buffer[:sof]
            if len(buffer) < 10:
                return
        payload_len = struct.unpack_from("<H", buffer, 6)[0]
        if payload_len > 1024:
            del buffer[0]
            continue
        total = 8 + payload_len + 2
        if len(buffer) < total:
            return
        expected = struct.unpack_from("<H", buffer, 8 + payload_len)[0]
        actual = crc16_ccitt(bytes(buffer[2 : 8 + payload_len]))
        if expected != actual:
            del buffer[0]
            continue
        cmd = buffer[3]
        seq = struct.unpack_from("<H", buffer, 4)[0]
        payload = bytes(buffer[8 : 8 + payload_len])
        del buffer[:total]
        yield cmd, seq, payload


def infer_dtype(name: str) -> DataType:
    lname = name.lower()
    if "_u1_" in lname or lname.startswith("u1_"):
        return DataType.UInt8
    if "_u2_" in lname or lname.startswith("u2_"):
        return DataType.UInt16
    if "_u4_" in lname or lname.startswith("u4_"):
        return DataType.UInt32
    if "_s1_" in lname or lname.startswith("s1_"):
        return DataType.Int8
    if "_s2_" in lname or lname.startswith("s2_"):
        return DataType.Int16
    if "_s4_" in lname or lname.startswith("s4_"):
        return DataType.Int32
    if "_f8_" in lname or "double" in lname:
        return DataType.Float64
    return DataType.Float32


def load_vars_from_map(map_path: Path, prefixes: Sequence[str], max_vars: int, min_addr: int) -> List[Variable]:
    text = map_path.read_text(encoding="cp932", errors="ignore")
    lines = text.splitlines()
    vars_: List[Variable] = []
    symbol_re = re.compile(r"^\s+(_[A-Za-z0-9_$.@]+)\s*$")
    addr_re = re.compile(r"^\s+([0-9A-Fa-f]{8})\s+([0-9A-Fa-f]+)\s+data\s*,g")
    current_symbol: str | None = None

    prefix_list = [p.lower() for p in prefixes]
    for line in lines:
        m1 = symbol_re.match(line)
        if m1:
            current_symbol = m1.group(1)
            continue

        if current_symbol is None:
            continue

        m2 = addr_re.match(line)
        if not m2:
            continue

        symbol = current_symbol.lstrip("_")
        current_symbol = None
        lower_symbol = symbol.lower()
        if prefix_list and not any(lower_symbol.startswith(p) for p in prefix_list):
            continue

        addr = int(m2.group(1), 16)
        if addr < min_addr:
            continue

        if "table" in lower_symbol or "vect" in lower_symbol:
            continue

        dtype = infer_dtype(symbol)
        unit = "raw"
        if "speed" in lower_symbol:
            unit = "rpm"
        elif "temp" in lower_symbol:
            unit = "C"
        elif "volt" in lower_symbol:
            unit = "V"
        elif "curr" in lower_symbol:
            unit = "A"
        vars_.append(Variable(name=symbol, address=addr, dtype=dtype, unit=unit, value=0.0))
        if len(vars_) >= max_vars:
            break

    return vars_


def default_vars() -> List[Variable]:
    return [
        Variable("g_motor_speed", 0x20001000, DataType.Float32, 1.0, "rpm", 1200.0),
        Variable("g_bus_voltage", 0x20001004, DataType.Float32, 1.0, "V", 24.2),
        Variable("g_temp", 0x20001008, DataType.Float32, 1.0, "C", 36.5),
        Variable("g_iq_ref", 0x2000100C, DataType.Float32, 1.0, "A", 1.2),
    ]


def encode_var_table_text(vars_: Sequence[Variable]) -> bytes:
    parts = [f"{v.name},0x{v.address:08X},{v.dtype.name.lower()},{v.scale:.6g},{v.unit}" for v in vars_]
    text = ";".join(parts)
    return text.encode("ascii", errors="ignore")[:1024]


def encode_var_table_binary(vars_: Sequence[Variable]) -> bytes:
    out = bytearray()
    out.extend(struct.pack("<H", len(vars_)))
    for v in vars_:
        name = v.name.encode("ascii", errors="ignore")[:64]
        unit = v.unit.encode("ascii", errors="ignore")[:16]
        item = bytearray()
        item.extend(struct.pack("<I", v.address))
        item.append(int(v.dtype))
        item.extend(struct.pack("<H", 1))
        item.extend(struct.pack("<f", float(v.scale)))
        item.append(len(unit))
        item.append(len(name))
        item.extend(unit)
        item.extend(name)
        if len(out) + len(item) > 1024:
            break
        out.extend(item)
    # patch count based on actual encoded entries
    encoded_count = 0
    idx = 2
    while idx < len(out):
        if idx + 4 + 1 + 2 + 4 + 1 + 1 > len(out):
            break
        unit_len = out[idx + 11]
        name_len = out[idx + 12]
        step = 13 + unit_len + name_len
        if idx + step > len(out):
            break
        encoded_count += 1
        idx += step
    struct.pack_into("<H", out, 0, encoded_count)
    return bytes(out)


def encode_readmem_text(values: Sequence[Variable]) -> bytes:
    text = ",".join(f"0x{v.address:08X}={v.value:.6f}" for v in values)
    return text.encode("ascii", errors="ignore")[:1024]


def encode_readmem_binary(values: Sequence[Variable]) -> bytes:
    out = bytearray()
    out.extend(struct.pack("<H", len(values)))
    for v in values:
        if v.dtype in (DataType.UInt8, DataType.Int8):
            raw = struct.pack("<B", int(v.value) & 0xFF)
        elif v.dtype in (DataType.UInt16, DataType.Int16):
            raw = struct.pack("<H", int(v.value) & 0xFFFF)
        elif v.dtype in (DataType.UInt32, DataType.Int32):
            raw = struct.pack("<I", int(v.value) & 0xFFFFFFFF)
        elif v.dtype == DataType.Float64:
            raw = struct.pack("<d", float(v.value))
        else:
            raw = struct.pack("<f", float(v.value))
        item = struct.pack("<IH", v.address, len(raw)) + raw
        if len(out) + len(item) > 1024:
            break
        out.extend(item)
    return bytes(out)


def parse_readmem_req(payload: bytes) -> List[tuple[int, int]]:
    reqs: List[tuple[int, int]] = []
    i = 0
    while i + 6 <= len(payload):
        addr = struct.unpack_from("<I", payload, i)[0]
        size = struct.unpack_from("<H", payload, i + 4)[0]
        reqs.append((addr, size))
        i += 6
    return reqs


def parse_writemem(payload: bytes) -> List[tuple[int, bytes]]:
    wrs: List[tuple[int, bytes]] = []
    i = 0

    # New format: repeat [addr:u32][size:u16][raw:size]
    while i + 6 <= len(payload):
        addr = struct.unpack_from("<I", payload, i)[0]
        size = struct.unpack_from("<H", payload, i + 4)[0]
        i += 6
        if i + size > len(payload):
            wrs.clear()
            break
        wrs.append((addr, bytes(payload[i : i + size])))
        i += size

    if wrs:
        return wrs

    # Legacy fallback: repeat [addr:u32][value:f32]
    i = 0
    while i + 8 <= len(payload):
        addr = struct.unpack_from("<I", payload, i)[0]
        value = struct.pack("<f", struct.unpack_from("<f", payload, i + 4)[0])
        wrs.append((addr, value))
        i += 8
    return wrs


class UartMcuSim:
    def __init__(self, args: argparse.Namespace):
        self.args = args
        self.port = serial.Serial(args.port, args.baud, timeout=0.01, write_timeout=0.05)
        self.rx_buf = bytearray()
        self.tx_seq = 1
        self.stream_enabled = args.auto_stream
        self.running = True
        self.stats_tx_frames = 0
        self.stats_rx_frames = 0
        self.stats_crc_err = 0
        self.last_stat_print = time.perf_counter()
        self.start_time = time.perf_counter()
        self.channel_count = max(1, args.channels)
        self.stream_hz = max(1.0, args.stream_hz)
        self.drop_rate = max(0.0, min(1.0, args.drop_rate))
        self.crc_error_rate = max(0.0, min(1.0, args.crc_error_rate))
        self.var_table_format = args.var_table_format
        self.readmem_format = args.readmem_format
        self.vars = self._build_vars()
        self.var_by_addr: Dict[int, Variable] = {v.address: v for v in self.vars}
        self.write_timeout_count = 0

    def _build_vars(self) -> List[Variable]:
        if self.args.map_file:
            try:
                vars_ = load_vars_from_map(
                    Path(self.args.map_file),
                    [p.strip() for p in self.args.map_prefix.split(",") if p.strip()],
                    self.args.map_max_vars,
                    self.args.map_min_addr,
                )
                if vars_:
                    print(f"[SIM] loaded {len(vars_)} vars from map: {self.args.map_file}")
                    return vars_
                print("[SIM] map parsed but no matching vars found, fallback to defaults")
            except Exception as ex:
                print(f"[SIM] map parse failed: {ex}, fallback to defaults")
        return default_vars()

    def next_seq(self) -> int:
        v = self.tx_seq
        self.tx_seq = (self.tx_seq + 1) & 0xFFFF
        return v

    def send_rforge(self, cmd: CommandId, payload: bytes):
        if self.drop_rate > 0 and random.random() < self.drop_rate:
            return
        pkt = build_rforge_frame(int(cmd), self.next_seq(), payload, self.crc_error_rate)
        try:
            self.port.write(pkt)
            self.stats_tx_frames += 1
        except serial.SerialTimeoutException:
            # Backpressure is expected at high stream rates; keep simulator alive.
            self.write_timeout_count += 1
        except serial.SerialException:
            self.write_timeout_count += 1

    def send_stream_frame(self):
        t = time.perf_counter() - self.start_time
        ts_us = int(time.time() * 1_000_000)
        payload = bytearray(struct.pack("<Q", ts_us))
        for ch in range(self.channel_count):
            base = math.sin(2 * math.pi * (0.8 + ch * 0.11) * t)
            mod = 0.35 * math.sin(2 * math.pi * 0.07 * t + ch * 0.2)
            value = 0.7 * base + mod + ch * 0.03
            payload.extend(struct.pack("<Hf", ch, value))
        self.send_rforge(CommandId.StreamData, bytes(payload))

    def send_stream_vofa(self):
        t = time.perf_counter() - self.start_time
        vals = []
        for ch in range(self.channel_count):
            base = math.sin(2 * math.pi * (0.8 + ch * 0.11) * t)
            mod = 0.35 * math.sin(2 * math.pi * 0.07 * t + ch * 0.2)
            vals.append(f"{0.7 * base + mod + ch * 0.03:.6f}")
        line = ",".join(vals) + "\n"
        if self.drop_rate == 0.0 or random.random() >= self.drop_rate:
            try:
                self.port.write(line.encode("ascii"))
                self.stats_tx_frames += 1
            except serial.SerialTimeoutException:
                self.write_timeout_count += 1
            except serial.SerialException:
                self.write_timeout_count += 1

    def stream_worker(self):
        period = 1.0 / self.stream_hz
        next_deadline = time.perf_counter()
        while self.running:
            if self.stream_enabled:
                if self.args.protocol == "rforge":
                    self.send_stream_frame()
                else:
                    self.send_stream_vofa()
            next_deadline += period
            delay = next_deadline - time.perf_counter()
            if delay > 0:
                time.sleep(delay)
            else:
                # If producer falls behind, drop schedule debt and continue.
                next_deadline = time.perf_counter()

    def tick_vars(self):
        t = time.perf_counter() - self.start_time
        for idx, v in enumerate(self.vars):
            if "speed" in v.name.lower():
                v.value = 1500 + 220 * math.sin(2 * math.pi * 0.4 * t)
            elif "temp" in v.name.lower():
                v.value = 35 + 4 * math.sin(2 * math.pi * 0.02 * t + idx)
            elif "volt" in v.name.lower():
                v.value = 24 + 0.6 * math.sin(2 * math.pi * 0.7 * t)
            elif "curr" in v.name.lower() or "iq" in v.name.lower():
                v.value = 1.2 + 0.25 * math.sin(2 * math.pi * 1.2 * t + idx)
            else:
                v.value = 0.5 * math.sin(2 * math.pi * 0.5 * t + idx)

    def on_frame(self, cmd: int, seq: int, payload: bytes):
        self.stats_rx_frames += 1
        if self.args.echo_rx:
            print(f"[RX] cmd=0x{cmd:02X} seq={seq} len={len(payload)}")

        if cmd == CommandId.Ping:
            self.send_rforge(CommandId.Ack, b"")
        elif cmd == CommandId.StreamStart:
            self.stream_enabled = True
            self.send_rforge(CommandId.Ack, b"")
        elif cmd == CommandId.StreamStop:
            self.stream_enabled = False
            self.send_rforge(CommandId.Ack, b"")
        elif cmd == CommandId.GetVarTable:
            if self.var_table_format == "binary":
                payload_out = encode_var_table_binary(self.vars)
            else:
                payload_out = encode_var_table_text(self.vars)
            self.send_rforge(CommandId.GetVarTable, payload_out)
        elif cmd == CommandId.ReadMemBatch:
            reqs = parse_readmem_req(payload)
            vals = []
            for addr, _size in reqs:
                if addr in self.var_by_addr:
                    vals.append(self.var_by_addr[addr])
            if self.readmem_format == "binary":
                payload_out = encode_readmem_binary(vals)
            else:
                payload_out = encode_readmem_text(vals)
            self.send_rforge(CommandId.ReadMemBatch, payload_out)
        elif cmd == CommandId.WriteMem:
            for addr, raw in parse_writemem(payload):
                if addr not in self.var_by_addr:
                    continue

                v = self.var_by_addr[addr]
                try:
                    if v.dtype == DataType.Int8 and len(raw) >= 1:
                        v.value = float(struct.unpack_from("<b", raw, 0)[0])
                    elif v.dtype == DataType.UInt8 and len(raw) >= 1:
                        v.value = float(struct.unpack_from("<B", raw, 0)[0])
                    elif v.dtype == DataType.Int16 and len(raw) >= 2:
                        v.value = float(struct.unpack_from("<h", raw, 0)[0])
                    elif v.dtype == DataType.UInt16 and len(raw) >= 2:
                        v.value = float(struct.unpack_from("<H", raw, 0)[0])
                    elif v.dtype == DataType.Int32 and len(raw) >= 4:
                        v.value = float(struct.unpack_from("<i", raw, 0)[0])
                    elif v.dtype == DataType.UInt32 and len(raw) >= 4:
                        v.value = float(struct.unpack_from("<I", raw, 0)[0])
                    elif v.dtype == DataType.Float64 and len(raw) >= 8:
                        v.value = float(struct.unpack_from("<d", raw, 0)[0])
                    elif len(raw) >= 4:
                        v.value = float(struct.unpack_from("<f", raw, 0)[0])
                except struct.error:
                    pass
            self.send_rforge(CommandId.Ack, b"")
        elif cmd == CommandId.SetStreamConfig and len(payload) >= 2:
            # Simulator convention: [channel_count, stream_hz(Hz) low-byte].
            self.channel_count = max(1, payload[0])
            self.stream_hz = max(1.0, float(payload[1]))
            self.send_rforge(CommandId.Ack, b"")

    def print_stats(self):
        now = time.perf_counter()
        if now - self.last_stat_print < 1.0:
            return
        elapsed = now - self.last_stat_print
        tx_rate = self.stats_tx_frames / elapsed
        rx_rate = self.stats_rx_frames / elapsed
        print(
            f"[SIM] tx={tx_rate:7.1f} fps  rx={rx_rate:6.1f} fps  "
            f"stream={'on' if self.stream_enabled else 'off'}  "
            f"wto={self.write_timeout_count}  "
            f"vars={len(self.vars)}"
        )
        self.stats_tx_frames = 0
        self.stats_rx_frames = 0
        self.last_stat_print = now

    def run(self):
        print(
            f"[SIM] open={self.args.port} baud={self.args.baud} protocol={self.args.protocol} "
            f"hz={self.stream_hz} ch={self.channel_count}"
        )
        streamer = threading.Thread(target=self.stream_worker, daemon=True)
        streamer.start()
        try:
            while self.running:
                self.tick_vars()
                data = self.port.read(4096)
                if data and self.args.protocol == "rforge":
                    self.rx_buf.extend(data)
                    for cmd, seq, payload in iter_rforge_frames(self.rx_buf):
                        self.on_frame(cmd, seq, payload)
                self.print_stats()
        except KeyboardInterrupt:
            print("[SIM] stopping...")
        finally:
            self.running = False
            time.sleep(0.05)
            self.port.close()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="RenesasForge UART MCU simulator")
    parser.add_argument("--port", required=True, help="serial port, e.g. COM8")
    parser.add_argument("--baud", type=int, default=921600, help="baud rate")
    parser.add_argument("--protocol", choices=["rforge", "vofa"], default="rforge")
    parser.add_argument("--channels", type=int, default=4, help="stream channel count")
    parser.add_argument("--stream-hz", type=float, default=200.0, help="stream frames per second")
    parser.add_argument("--auto-stream", action="store_true", help="enable stream immediately")
    parser.add_argument("--drop-rate", type=float, default=0.0, help="tx drop rate [0..1]")
    parser.add_argument("--crc-error-rate", type=float, default=0.0, help="rforge crc error inject [0..1]")
    parser.add_argument("--var-table-format", choices=["text", "binary"], default="text")
    parser.add_argument("--readmem-format", choices=["text", "binary"], default="text")
    parser.add_argument("--map-file", help="optional Renesas .map file path")
    parser.add_argument("--map-prefix", default="g_,com_,gui_", help="comma prefixes for map symbols")
    parser.add_argument("--map-max-vars", type=int, default=48, help="max vars imported from map")
    parser.add_argument("--map-min-addr", type=lambda x: int(x, 0), default=0x1000, help="min address filter, e.g. 0x1000")
    parser.add_argument("--echo-rx", action="store_true", help="print each parsed rx frame")
    return parser.parse_args()


def main():
    args = parse_args()
    sim = UartMcuSim(args)
    sim.run()


if __name__ == "__main__":
    main()
