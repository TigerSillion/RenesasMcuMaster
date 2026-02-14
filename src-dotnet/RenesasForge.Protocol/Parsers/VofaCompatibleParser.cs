using System.Text;
using RenesasForge.Core.Models;

namespace RenesasForge.Protocol.Parsers;

public sealed class VofaCompatibleParser : IProtocolParser
{
    private readonly List<byte> _buffer = new();
    private readonly Queue<Frame> _frames = new();
    public ParserType Type => ParserType.VofaCompatible;

    public void Feed(ReadOnlySpan<byte> bytes)
    {
        _buffer.AddRange(bytes.ToArray());
        while (true)
        {
            var idx = _buffer.IndexOf((byte)'\n');
            if (idx < 0) return;
            var line = Encoding.ASCII.GetString(_buffer.Take(idx).ToArray()).Trim();
            _buffer.RemoveRange(0, idx + 1);
            if (string.IsNullOrWhiteSpace(line)) continue;
            _frames.Enqueue(new Frame(CommandId.StreamData, 0, Encoding.ASCII.GetBytes(line)));
        }
    }

    public bool TryPopFrame(out Frame frame)
    {
        if (_frames.Count == 0) { frame = new Frame(CommandId.Unknown, 0, Array.Empty<byte>()); return false; }
        frame = _frames.Dequeue();
        return true;
    }

    public byte[] BuildCommand(CommandId cmd, ReadOnlySpan<byte> payload)
    {
        _ = cmd;
        return payload.ToArray().Concat(new byte[] { (byte)'\n' }).ToArray();
    }

    public void Reset() { _buffer.Clear(); _frames.Clear(); }
}
