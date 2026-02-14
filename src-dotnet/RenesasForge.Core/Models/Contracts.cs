namespace RenesasForge.Core.Models;

public sealed record TransportConfig(string PortName, int BaudRate = 921600, int DataBits = 8, int StopBits = 1, int Parity = 0);
public readonly record struct ChannelValue(ushort ChannelId, double Value);
public sealed record DataFrame(ulong TimestampUs, IReadOnlyList<ChannelValue> Channels);
public sealed record VariableDescriptor(string Name, uint Address, DataType Type, uint ArraySize, double Scale, string Unit);
public readonly record struct MemoryRequest(uint Address, ushort Size);
public sealed record RecordChunk(ulong StartTs, ulong EndTs, byte[] PackedSamples);
public sealed record Frame(CommandId Cmd, ushort Seq, byte[] Payload);
