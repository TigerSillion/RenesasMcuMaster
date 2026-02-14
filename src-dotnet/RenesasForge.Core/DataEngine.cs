using RenesasForge.Core.Models;

namespace RenesasForge.Core;

public sealed class DataEngine
{
    private readonly LinkedList<DataFrame> _frames = new();
    private readonly object _sync = new();
    private readonly int _maxFrames;

    public DataEngine(int maxFrames = 4096) { _maxFrames = maxFrames; }

    public event Action<DataFrame>? DataFrameReady;

    public void Append(DataFrame frame)
    {
        lock (_sync)
        {
            _frames.AddLast(frame);
            while (_frames.Count > _maxFrames) _frames.RemoveFirst();
        }

        DataFrameReady?.Invoke(frame);
    }

    public IReadOnlyList<DataFrame> RecentFrames(int maxCount)
    {
        if (maxCount <= 0) return Array.Empty<DataFrame>();

        lock (_sync)
        {
            return _frames.Reverse().Take(maxCount).Reverse().ToArray();
        }
    }

    public bool TryGetChannelStats(ushort channelId, int maxFrames, out ChannelStats stats)
    {
        stats = default;
        var frames = RecentFrames(maxFrames);
        if (frames.Count == 0) return false;

        var values = new List<(ulong Ts, double Value)>(frames.Count);
        foreach (var frame in frames)
        {
            foreach (var ch in frame.Channels)
            {
                if (ch.ChannelId == channelId) values.Add((frame.TimestampUs, ch.Value));
            }
        }

        if (values.Count == 0) return false;

        var min = values[0].Value;
        var max = values[0].Value;
        var sum = 0.0;
        foreach (var (_, value) in values)
        {
            sum += value;
            if (value < min) min = value;
            if (value > max) max = value;
        }

        var mean = sum / values.Count;
        var freqHz = EstimateFrequency(values);
        stats = new ChannelStats(values.Count, mean, max - min, freqHz);
        return true;
    }

    private static double EstimateFrequency(IReadOnlyList<(ulong Ts, double Value)> values)
    {
        if (values.Count < 3) return 0;

        var crossings = new List<ulong>();
        for (var i = 1; i < values.Count; i++)
        {
            var prev = values[i - 1].Value;
            var current = values[i].Value;
            if (prev <= 0 && current > 0) crossings.Add(values[i].Ts);
        }

        if (crossings.Count < 2) return 0;
        var totalUs = crossings[^1] - crossings[0];
        if (totalUs == 0) return 0;

        var cycles = crossings.Count - 1;
        return cycles * 1_000_000.0 / totalUs;
    }
}

public readonly record struct ChannelStats(int SampleCount, double Mean, double PeakToPeak, double FrequencyHz);
