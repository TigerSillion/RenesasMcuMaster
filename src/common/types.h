#pragma once
#include <QtCore/QByteArray>
#include <QtCore/QDateTime>
#include <QtCore/QString>
#include <QtCore/QVector>
#include <cstdint>
namespace rf {
enum class ConnectionState { Disconnected, Connecting, Connected, Error };
enum class ParserType { AutoDetect, RForgeBinary, VofaCompatible };
enum class CommandId : uint8_t {
    Ping = 0x01, Ack = 0x02, StreamStart = 0x03, StreamStop = 0x04,
    SetStreamConfig = 0x05, GetVarTable = 0x10, ReadMemBatch = 0x11,
    WriteMem = 0x12, StreamData = 0x20, Unknown = 0xFF
};
enum class DataType : uint8_t { Int8, UInt8, Int16, UInt16, Int32, UInt32, Float32, Float64 };
struct TransportConfig { QString portName; int baudRate = 921600; int dataBits = 8; int stopBits = 1; int parity = 0; };
struct ChannelValue { uint16_t channel_id = 0; double value = 0.0; };
struct DataFrame { uint64_t timestamp_us = 0; QVector<ChannelValue> channels; };
struct VariableDescriptor { QString name; uint32_t address = 0; DataType type = DataType::Float32; uint32_t array_size = 1; double scale = 1.0; QString unit; };
struct MemoryRequest { uint32_t addr = 0; uint16_t size = 0; };
struct RecordChunk { uint64_t start_ts = 0; uint64_t end_ts = 0; QByteArray packed_samples; };
struct Frame { CommandId cmd = CommandId::Unknown; uint16_t seq = 0; QByteArray payload; };
inline uint64_t nowUs() { return static_cast<uint64_t>(QDateTime::currentMSecsSinceEpoch()) * 1000ULL; }
}
