namespace RenesasForge.Core.Models;

public enum ConnectionState { Disconnected, Connecting, Connected, Error }
public enum ParserType { AutoDetect, RForgeBinary, VofaCompatible }
public enum CommandId : byte {
    Ping = 0x01, Ack = 0x02, StreamStart = 0x03, StreamStop = 0x04,
    SetStreamConfig = 0x05, GetVarTable = 0x10, ReadMemBatch = 0x11,
    WriteMem = 0x12, StreamData = 0x20, Unknown = 0xFF
}
public enum DataType : byte { Int8, UInt8, Int16, UInt16, Int32, UInt32, Float32, Float64 }
