using RenesasForge.Core.Models;

namespace RenesasForge.Protocol;

public interface IProtocolParser
{
    ParserType Type { get; }
    void Feed(ReadOnlySpan<byte> bytes);
    bool TryPopFrame(out Frame frame);
    byte[] BuildCommand(CommandId cmd, ReadOnlySpan<byte> payload);
    void Reset();
}
