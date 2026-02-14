using RenesasForge.Core.Models;

namespace RenesasForge.Protocol.Parsers;

public sealed class RForgeBinaryParser : IProtocolParser
{
    private const byte Sof0 = 0xAA;
    private const byte Sof1 = 0x55;
    private const int HeaderSize = 8;
    private const int TailSize = 2;
    private const int MaxPayload = 1024;

    private readonly List<byte> _buffer = new();
    private readonly Queue<Frame> _frames = new();
    private ushort _nextSeq;

    public ParserType Type => ParserType.RForgeBinary;
    public int CrcErrorCount { get; private set; }
    public int OversizePayloadCount { get; private set; }

    public void Feed(ReadOnlySpan<byte> bytes)
    {
        _buffer.AddRange(bytes.ToArray());

        while (TryExtractNextFrame()) { }
    }

    public bool TryPopFrame(out Frame frame)
    {
        if (_frames.Count == 0)
        {
            frame = new Frame(CommandId.Unknown, 0, Array.Empty<byte>());
            return false;
        }

        frame = _frames.Dequeue();
        return true;
    }

    public byte[] BuildCommand(CommandId cmd, ReadOnlySpan<byte> payload)
    {
        var seq = _nextSeq++;
        var packet = new byte[HeaderSize + payload.Length + TailSize];
        packet[0] = Sof0;
        packet[1] = Sof1;
        packet[2] = 1;
        packet[3] = (byte)cmd;
        packet[4] = (byte)(seq & 0xFF);
        packet[5] = (byte)(seq >> 8);
        packet[6] = (byte)(payload.Length & 0xFF);
        packet[7] = (byte)(payload.Length >> 8);
        payload.CopyTo(packet.AsSpan(HeaderSize));

        var crc = Crc16Ccitt.Compute(packet.AsSpan(2, 1 + 1 + 2 + 2 + payload.Length));
        packet[HeaderSize + payload.Length] = (byte)(crc & 0xFF);
        packet[HeaderSize + payload.Length + 1] = (byte)(crc >> 8);
        return packet;
    }

    public void Reset()
    {
        _buffer.Clear();
        _frames.Clear();
        CrcErrorCount = 0;
        OversizePayloadCount = 0;
    }

    private bool TryExtractNextFrame()
    {
        if (_buffer.Count < HeaderSize + TailSize) return false;

        var sofIndex = FindSof();
        if (sofIndex < 0)
        {
            _buffer.Clear();
            return false;
        }

        if (sofIndex > 0) _buffer.RemoveRange(0, sofIndex);
        if (_buffer.Count < HeaderSize + TailSize) return false;

        var payloadLen = (ushort)(_buffer[6] | (_buffer[7] << 8));
        if (payloadLen > MaxPayload)
        {
            OversizePayloadCount++;
            _buffer.RemoveAt(0);
            return true;
        }

        var totalLen = HeaderSize + payloadLen + TailSize;
        if (_buffer.Count < totalLen) return false;

        var crcInput = new byte[1 + 1 + 2 + 2 + payloadLen];
        _buffer.CopyTo(2, crcInput, 0, crcInput.Length);
        var expected = (ushort)(_buffer[HeaderSize + payloadLen] | (_buffer[HeaderSize + payloadLen + 1] << 8));
        var actual = Crc16Ccitt.Compute(crcInput);
        if (expected != actual)
        {
            CrcErrorCount++;
            _buffer.RemoveAt(0);
            return true;
        }

        var cmd = (CommandId)_buffer[3];
        var seq = (ushort)(_buffer[4] | (_buffer[5] << 8));
        var payload = new byte[payloadLen];
        if (payloadLen > 0) _buffer.CopyTo(HeaderSize, payload, 0, payloadLen);
        _frames.Enqueue(new Frame(cmd, seq, payload));
        _buffer.RemoveRange(0, totalLen);
        return true;
    }

    private int FindSof()
    {
        for (var i = 0; i + 1 < _buffer.Count; i++)
        {
            if (_buffer[i] == Sof0 && _buffer[i + 1] == Sof1) return i;
        }

        return -1;
    }
}
