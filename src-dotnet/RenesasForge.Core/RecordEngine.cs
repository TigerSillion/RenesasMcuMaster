using System.Text;
using RenesasForge.Core.Models;

namespace RenesasForge.Core;

public sealed class RecordEngine
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RFR1");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileStream? _stream;
    private int _pendingChunks;

    public bool IsRecording => _stream is not null;
    public string? CurrentRecordPath { get; private set; }

    public async Task<bool> StartAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        await _gate.WaitAsync(ct);
        try
        {
            await CloseInternalAsync();
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            _stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await _stream.WriteAsync(Magic, ct);
            _pendingChunks = 0;
            CurrentRecordPath = fullPath;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CloseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseInternalAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AppendChunkAsync(RecordChunk chunk, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_stream is null) return false;

            await _stream.WriteAsync(BitConverter.GetBytes(chunk.StartTs), ct);
            await _stream.WriteAsync(BitConverter.GetBytes(chunk.EndTs), ct);
            await _stream.WriteAsync(BitConverter.GetBytes(chunk.PackedSamples.Length), ct);
            await _stream.WriteAsync(chunk.PackedSamples, ct);
            _pendingChunks++;
            if (_pendingChunks >= 32)
            {
                await _stream.FlushAsync(ct);
                _pendingChunks = 0;
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async IAsyncEnumerable<RecordChunk> ReadChunksAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        var magic = new byte[Magic.Length];
        if (await stream.ReadAsync(magic, ct) != Magic.Length || !magic.AsSpan().SequenceEqual(Magic)) yield break;

        var u64 = new byte[8];
        var u32 = new byte[4];
        while (true)
        {
            if (!await ReadExactAsync(stream, u64, ct)) yield break;
            var startTs = BitConverter.ToUInt64(u64);
            if (!await ReadExactAsync(stream, u64, ct)) yield break;
            var endTs = BitConverter.ToUInt64(u64);
            if (!await ReadExactAsync(stream, u32, ct)) yield break;
            var len = BitConverter.ToInt32(u32);
            if (len < 0 || len > 4 * 1024 * 1024) yield break;

            var payload = new byte[len];
            if (!await ReadExactAsync(stream, payload, ct)) yield break;

            yield return new RecordChunk(startTs, endTs, payload);
        }
    }

    public async IAsyncEnumerable<DataFrame> ReadFramesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in ReadChunksAsync(path, ct))
        {
            if (StreamFrameCodec.TryParsePayload(chunk.PackedSamples, out var frame, chunk.StartTs)) yield return frame;
        }
    }

    public async Task<IReadOnlyList<DataFrame>> LoadFramesAsync(string path, CancellationToken ct)
    {
        var frames = new List<DataFrame>();
        await foreach (var frame in ReadFramesAsync(path, ct))
        {
            ct.ThrowIfCancellationRequested();
            frames.Add(frame);
        }

        return frames;
    }

    public async Task<bool> ExportCsvAsync(string path, IEnumerable<DataFrame> frames, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        await using var writer = new StreamWriter(fullPath, false, new UTF8Encoding(false));
        await writer.WriteLineAsync("timestamp_us,channel_id,value");
        foreach (var frame in frames)
        {
            foreach (var channel in frame.Channels)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync($"{frame.TimestampUs},{channel.ChannelId},{channel.Value}");
            }
        }

        return true;
    }

    public async Task<bool> ExportCsvFromRecordAsync(string recordPath, string csvPath, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(csvPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        await using var writer = new StreamWriter(fullPath, false, new UTF8Encoding(false));
        await writer.WriteLineAsync("timestamp_us,channel_id,value");

        var exportedRows = 0;
        await foreach (var frame in ReadFramesAsync(recordPath, ct))
        {
            foreach (var channel in frame.Channels)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync($"{frame.TimestampUs},{channel.ChannelId},{channel.Value}");
                exportedRows++;
            }
        }

        return exportedRows > 0;
    }

    private async Task CloseInternalAsync()
    {
        if (_stream is null) return;
        await _stream.FlushAsync();
        await _stream.DisposeAsync();
        _stream = null;
        _pendingChunks = 0;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
            if (read <= 0) return false;
            totalRead += read;
        }

        return true;
    }
}
