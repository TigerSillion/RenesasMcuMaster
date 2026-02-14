using RenesasForge.Core.Models;

namespace RenesasForge.Core.Abstractions;

public interface ITransport : IDisposable
{
    event Action? DataReceived;
    event Action<string>? ErrorOccurred;
    event Action<ConnectionState>? StateChanged;

    bool IsOpen { get; }
    int BytesAvailable { get; }

    Task<bool> OpenAsync(TransportConfig cfg, CancellationToken ct);
    Task CloseAsync();
    Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    int Read(Span<byte> buffer);
}
