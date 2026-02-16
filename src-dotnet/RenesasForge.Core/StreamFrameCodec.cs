using System.Globalization;
using System.Text;
using RenesasForge.Core.Models;

namespace RenesasForge.Core;

public static class StreamFrameCodec
{
    public static bool TryParsePayload(ReadOnlySpan<byte> payload, out DataFrame frame, ulong fallbackTimestampUs = 0)
    {
        if (LooksLikeVofaCsv(payload) && TryParseVofaPayload(payload, out frame, fallbackTimestampUs)) return true;
        if (TryParseBinaryPayload(payload, out frame)) return true;
        return TryParseVofaPayload(payload, out frame, fallbackTimestampUs);
    }

    public static bool TryParseBinaryPayload(ReadOnlySpan<byte> payload, out DataFrame frame)
    {
        frame = new DataFrame(0, Array.Empty<ChannelValue>());
        if (payload.Length < 14 || (payload.Length - 8) % 6 != 0) return false;

        var timestampUs = BitConverter.ToUInt64(payload.Slice(0, 8));
        var channels = new List<ChannelValue>((payload.Length - 8) / 6);
        for (var i = 8; i + 5 < payload.Length; i += 6)
        {
            var channelId = BitConverter.ToUInt16(payload.Slice(i, 2));
            var value = BitConverter.ToSingle(payload.Slice(i + 2, 4));
            channels.Add(new ChannelValue(channelId, value));
        }

        if (channels.Count == 0) return false;
        frame = new DataFrame(timestampUs, channels);
        return true;
    }

    public static bool TryParseVofaPayload(ReadOnlySpan<byte> payload, out DataFrame frame, ulong fallbackTimestampUs = 0)
    {
        frame = new DataFrame(0, Array.Empty<ChannelValue>());
        if (payload.Length == 0) return false;

        var line = Encoding.ASCII.GetString(payload).Trim();
        if (string.IsNullOrWhiteSpace(line)) return false;

        var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var channels = new List<ChannelValue>(parts.Length);
        ushort channelId = 0;
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) continue;
            channels.Add(new ChannelValue(channelId++, value));
        }

        if (channels.Count == 0) return false;
        var timestampUs = fallbackTimestampUs != 0
            ? fallbackTimestampUs
            : (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000UL;
        frame = new DataFrame(timestampUs, channels);
        return true;
    }

    public static byte[] EncodeBinaryPayload(DataFrame frame)
    {
        var payload = new byte[8 + frame.Channels.Count * 6];
        BitConverter.GetBytes(frame.TimestampUs).CopyTo(payload, 0);
        var offset = 8;
        foreach (var channel in frame.Channels)
        {
            BitConverter.GetBytes(channel.ChannelId).CopyTo(payload, offset);
            BitConverter.GetBytes((float)channel.Value).CopyTo(payload, offset + 2);
            offset += 6;
        }

        return payload;
    }

    private static bool LooksLikeVofaCsv(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0 || !payload.Contains((byte)',')) return false;
        foreach (var b in payload)
        {
            if (b == (byte)'\r' || b == (byte)'\n' || b == (byte)'\t') continue;
            if (b < 0x20 || b > 0x7E) return false;
        }

        return true;
    }
}
