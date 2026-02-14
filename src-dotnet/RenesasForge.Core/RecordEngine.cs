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

    public async Task<bool> StartAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await CloseInternalAsync();
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await _stream.WriteAsync(Magic, ct);
            _pendingChunks = 0;
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

    public async Task<bool> ExportCsvAsync(string path, IEnumerable<DataFrame> frames, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, false, Encoding.UTF8);
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
