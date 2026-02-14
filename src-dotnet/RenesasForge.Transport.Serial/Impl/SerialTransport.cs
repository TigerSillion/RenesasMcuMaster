using System.IO.Ports;
using RenesasForge.Core.Abstractions;
using RenesasForge.Core.Models;

namespace RenesasForge.Transport.Serial.Impl;

public sealed class SerialTransport : ITransport
{
    private readonly object _sync = new();
    private SerialPort? _serial;

    public event Action? DataReceived;
    public event Action<string>? ErrorOccurred;
    public event Action<ConnectionState>? StateChanged;

    public bool IsOpen
    {
        get
        {
            lock (_sync)
            {
                return _serial?.IsOpen == true;
            }
        }
    }

    public int BytesAvailable
    {
        get
        {
            lock (_sync)
            {
                return _serial?.BytesToRead ?? 0;
            }
        }
    }

    public Task<bool> OpenAsync(TransportConfig cfg, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            try
            {
                lock (_sync)
                {
                    CloseInternalLocked();
                    StateChanged?.Invoke(ConnectionState.Connecting);
                    _serial = new SerialPort(cfg.PortName, cfg.BaudRate)
                    {
                        DataBits = cfg.DataBits,
                        StopBits = cfg.StopBits == 2 ? StopBits.Two : StopBits.One,
                        Parity = (Parity)cfg.Parity,
                        ReadTimeout = 50,
                        WriteTimeout = 250
                    };
                    _serial.DataReceived += OnDataReceived;
                    _serial.ErrorReceived += OnErrorReceived;
                    _serial.Open();
                }

                StateChanged?.Invoke(ConnectionState.Connected);
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
                StateChanged?.Invoke(ConnectionState.Error);
                return false;
            }
        }, ct);
    }

    public Task CloseAsync()
    {
        lock (_sync)
        {
            CloseInternalLocked();
        }

        StateChanged?.Invoke(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public async Task<int> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        SerialPort? serial;
        lock (_sync)
        {
            serial = _serial;
        }

        if (serial is null || !serial.IsOpen) return 0;
        try
        {
            await serial.BaseStream.WriteAsync(data, ct);
            return data.Length;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            StateChanged?.Invoke(ConnectionState.Error);
            return 0;
        }
    }

    public int Read(Span<byte> buffer)
    {
        SerialPort? serial;
        lock (_sync)
        {
            serial = _serial;
        }

        if (serial is null || !serial.IsOpen) return 0;
        try
        {
            return serial.BaseStream.Read(buffer);
        }
        catch (TimeoutException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
            StateChanged?.Invoke(ConnectionState.Error);
            return 0;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            CloseInternalLocked();
        }

        GC.SuppressFinalize(this);
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        _ = sender;
        _ = e;
        DataReceived?.Invoke();
    }

    private void OnErrorReceived(object? sender, SerialErrorReceivedEventArgs e)
    {
        _ = sender;
        ErrorOccurred?.Invoke($"Serial error: {e.EventType}");
        StateChanged?.Invoke(ConnectionState.Error);
    }

    private void CloseInternalLocked()
    {
        if (_serial is null) return;
        _serial.DataReceived -= OnDataReceived;
        _serial.ErrorReceived -= OnErrorReceived;
        if (_serial.IsOpen) _serial.Close();
        _serial.Dispose();
        _serial = null;
    }
}
