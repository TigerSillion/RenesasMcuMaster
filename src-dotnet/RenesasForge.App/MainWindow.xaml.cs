using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using RenesasForge.Core;
using RenesasForge.Core.Models;
using RenesasForge.Protocol;
using RenesasForge.Transport.Serial.Impl;
using ModelFrame = RenesasForge.Core.Models.Frame;
using IOPath = System.IO.Path;

namespace RenesasForge.App;

public partial class MainWindow : Window
{
    private readonly SerialTransport _transport = new();
    private readonly ParserManager _parser = new();
    private readonly DataEngine _dataEngine = new();
    private readonly VarEngine _varEngine = new();
    private readonly RecordEngine _recordEngine = new();

    private readonly DispatcherTimer _plotTimer;
    private readonly ConcurrentQueue<double> _sampleQueue = new();
    private readonly double[] _waveBuffer = new double[900];
    private readonly object _sampleSync = new();
    private readonly List<DataFrame> _replayFrames = new();

    private readonly ObservableCollection<VariableItem> _variables = new();
    private readonly Dictionary<uint, VariableItem> _variableMap = new();
    private readonly ObservableCollection<string> _logs = new();

    private TransportConfig? _lastConfig;
    private bool _manualDisconnect;
    private DateTime _lastStatsUpdate = DateTime.MinValue;
    private DateTime _replayAnchorWallClock = DateTime.MinValue;
    private ulong _replayAnchorTimestampUs;
    private int _replayIndex;
    private bool _isReplayPlaying;
    private string? _lastRecordPath;

    private int _frameCount;
    private int _sampleCount;
    private int _errorCount;

    public MainWindow()
    {
        InitializeComponent();

        VariableGrid.ItemsSource = _variables;
        LogListBox.ItemsSource = _logs;

        _transport.DataReceived += OnTransportData;
        _transport.ErrorOccurred += OnTransportError;
        _transport.StateChanged += OnTransportStateChanged;
        _dataEngine.DataFrameReady += OnDataFrameReady;

        RefreshPorts();
        SetParserMode(ParserType.AutoDetect);
        LoadMockVariables();

        _plotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _plotTimer.Tick += PlotTimerOnTick;
        _plotTimer.Start();

        RenderWave();
        Log("UI initialized.");
    }

    private void RefreshPortsButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshPorts();
    }

    private async void ConnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        var selected = PortComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected))
        {
            BottomStatusText.Text = "No serial port selected.";
            return;
        }

        _manualDisconnect = false;
        var baud = ParseBaudOrDefault();
        _lastConfig = new TransportConfig(selected, baud);
        var ok = await _transport.OpenAsync(_lastConfig, CancellationToken.None);
        BottomStatusText.Text = ok ? $"Connected to {selected}@{baud}" : $"Failed to connect to {selected}";
        Log(ok ? $"Connected {selected}@{baud}" : $"Connect failed {selected}");
    }

    private async void DisconnectButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _manualDisconnect = true;
        await _transport.CloseAsync();
        BottomStatusText.Text = "Disconnected.";
        Log("Disconnected.");
    }

    private async void PingButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await SendCommandAsync(CommandId.Ping, Array.Empty<byte>());
    }

    private async void StreamStartButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await SendCommandAsync(CommandId.StreamStart, Array.Empty<byte>());
    }

    private async void StreamStopButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await SendCommandAsync(CommandId.StreamStop, Array.Empty<byte>());
    }

    private async void GetVarTableButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await SendCommandAsync(CommandId.GetVarTable, Array.Empty<byte>());
    }

    private async void SetStreamConfigButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!byte.TryParse(StreamChannelTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var channelCount) || channelCount == 0)
        {
            BottomStatusText.Text = "Invalid stream channel count.";
            return;
        }

        if (!ushort.TryParse(StreamRateTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var streamHz) || streamHz == 0)
        {
            BottomStatusText.Text = "Invalid stream rate.";
            return;
        }

        // Protocol proposal: [channel_count:u8][reserved:u8][stream_hz:u16][flags:u16].
        var payload = new byte[6];
        payload[0] = channelCount;
        payload[1] = 0;
        BitConverter.GetBytes(streamHz).CopyTo(payload, 2);
        BitConverter.GetBytes((ushort)0).CopyTo(payload, 4);
        await SendCommandAsync(CommandId.SetStreamConfig, payload);
        Log($"SET_STREAM_CONFIG ch={channelCount} hz={streamHz}");
    }

    private async void StartRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var path = IOPath.Combine(Environment.CurrentDirectory, "build", "records", $"record_{DateTime.Now:yyyyMMdd_HHmmss}.rfr");
        var ok = await _recordEngine.StartAsync(path, CancellationToken.None);
        if (!ok)
        {
            BottomStatusText.Text = "Record start failed.";
            Log("Record start failed.");
            return;
        }

        _lastRecordPath = path;
        RecordStatusText.Text = "Record: On";
        BottomStatusText.Text = $"Recording to {path}";
        Log($"Record start: {path}");
    }

    private async void StopRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await _recordEngine.CloseAsync();
        RecordStatusText.Text = "Record: Off";
        BottomStatusText.Text = "Record stopped.";
        Log("Record stopped.");
    }

    private async void ExportCsvButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var path = IOPath.Combine(Environment.CurrentDirectory, "build", "exports", $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var exportedFromRecord = !string.IsNullOrWhiteSpace(_lastRecordPath) && File.Exists(_lastRecordPath);
        var ok = exportedFromRecord
            ? await _recordEngine.ExportCsvFromRecordAsync(_lastRecordPath!, path, CancellationToken.None)
            : await _recordEngine.ExportCsvAsync(path, _dataEngine.RecentFrames(int.MaxValue), CancellationToken.None);

        BottomStatusText.Text = ok ? $"CSV exported: {path}" : "CSV export failed.";
        Log(ok
            ? $"CSV exported ({(exportedFromRecord ? "record" : "memory")}): {path}"
            : "CSV export failed.");
    }

    private async void LoadRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        var initialDir = IOPath.Combine(Environment.CurrentDirectory, "build", "records");
        var dialog = new OpenFileDialog
        {
            Filter = "RenesasForge Record (*.rfr)|*.rfr|All Files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(initialDir) ? initialDir : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) != true) return;

        var frames = await _recordEngine.LoadFramesAsync(dialog.FileName, CancellationToken.None);
        _replayFrames.Clear();
        _replayFrames.AddRange(frames.OrderBy(x => x.TimestampUs));
        _replayIndex = 0;
        _isReplayPlaying = false;
        _lastRecordPath = dialog.FileName;

        if (_replayFrames.Count == 0)
        {
            BottomStatusText.Text = "No stream frames in selected record.";
            Log($"Replay load: no frames ({dialog.FileName})");
            return;
        }

        BottomStatusText.Text = $"Loaded replay frames: {_replayFrames.Count}";
        Log($"Replay loaded: {_replayFrames.Count} frame(s) from {dialog.FileName}");
    }

    private void PlayRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_replayFrames.Count == 0)
        {
            BottomStatusText.Text = "Load a record first.";
            return;
        }

        if (_replayIndex >= _replayFrames.Count) _replayIndex = 0;
        _isReplayPlaying = true;
        ResetReplayAnchor();
        BottomStatusText.Text = $"Replay playing @ {GetReplaySpeed():0.##}x";
        Log($"Replay play from frame {_replayIndex + 1}/{_replayFrames.Count}");
    }

    private void PauseRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!_isReplayPlaying) return;
        _isReplayPlaying = false;
        BottomStatusText.Text = "Replay paused.";
        Log("Replay paused.");
    }

    private void StopRecordPlaybackButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _isReplayPlaying = false;
        _replayIndex = 0;
        BottomStatusText.Text = "Replay stopped.";
        Log("Replay stopped.");
    }

    private void LoadMockVarsButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        LoadMockVariables();
        Log("Mock variable list loaded.");
    }

    private async void WriteSelectedVarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selectedItems = VariableGrid.SelectedItems.Cast<VariableItem>().ToArray();
        if (selectedItems.Length == 0)
        {
            BottomStatusText.Text = "Select a variable first.";
            return;
        }

        var payload = new List<byte>(selectedItems.Length * 8);
        foreach (var item in selectedItems)
        {
            if (!TryEncodeWriteValue(item, out var rawValue))
            {
                BottomStatusText.Text = $"Type encode failed: {item.Name}";
                return;
            }

            payload.AddRange(BitConverter.GetBytes(item.Address));
            payload.AddRange(BitConverter.GetBytes((ushort)rawValue.Length));
            payload.AddRange(rawValue);
        }

        await SendCommandAsync(CommandId.WriteMem, payload.ToArray());
        Log($"WRITE_MEM count={selectedItems.Length}");
    }

    private async void ReadSelectedVarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selectedItems = VariableGrid.SelectedItems.Cast<VariableItem>().ToArray();
        if (selectedItems.Length == 0)
        {
            BottomStatusText.Text = "Select a variable first.";
            return;
        }

        var payload = new List<byte>(selectedItems.Length * 6);
        foreach (var item in selectedItems)
        {
            payload.AddRange(BitConverter.GetBytes(item.Address));
            payload.AddRange(BitConverter.GetBytes(GetDataTypeSize(item.Type)));
        }

        await SendCommandAsync(CommandId.ReadMemBatch, payload.ToArray());
        Log($"READ_MEM_BATCH count={selectedItems.Length}");
    }

    private void ProtocolComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        var selected = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        SetParserMode(selected switch
        {
            "RForgeBinary" => ParserType.RForgeBinary,
            "VofaCompatible" => ParserType.VofaCompatible,
            _ => ParserType.AutoDetect
        });
    }

    private void WaveCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RenderWave();
    }

    private void RefreshPorts()
    {
        var selectedBefore = PortComboBox.SelectedItem as string;
        PortComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();

        if (!string.IsNullOrWhiteSpace(selectedBefore) && PortComboBox.Items.Contains(selectedBefore)) PortComboBox.SelectedItem = selectedBefore;
        else if (PortComboBox.Items.Count > 0) PortComboBox.SelectedIndex = 0;

        BottomStatusText.Text = $"Ports refreshed: {PortComboBox.Items.Count}";
    }

    private void SetParserMode(ParserType mode)
    {
        _parser.SetMode(mode);
        if (ProtocolStatusText is not null) ProtocolStatusText.Text = $"Parser: {_parser.ActiveType}";
        Log($"Parser mode -> {mode}");
    }

    private async Task SendCommandAsync(CommandId cmd, byte[] payload)
    {
        if (!_transport.IsOpen)
        {
            BottomStatusText.Text = "Not connected.";
            return;
        }

        var packet = _parser.BuildCommand(cmd, payload);
        var written = await _transport.WriteAsync(packet, CancellationToken.None);
        BottomStatusText.Text = $"Sent {cmd}, bytes={written}";
    }

    private void OnTransportData()
    {
        try
        {
            // Drain currently available bytes and let parser reconstruct complete frames.
            var available = _transport.BytesAvailable;
            if (available <= 0) return;

            var buffer = new byte[available];
            var read = _transport.Read(buffer.AsSpan());
            if (read <= 0) return;

            _parser.Feed(buffer.AsSpan(0, read));
            while (_parser.TryPopFrame(out var frame))
            {
                Interlocked.Increment(ref _frameCount);
                HandleFrame(frame);
            }

            Dispatcher.Invoke(() =>
            {
                FramesStatusText.Text = $"Frames: {_frameCount}";
                ProtocolStatusText.Text = $"Parser: {_parser.ActiveType}";
                ParserErrorStatusText.Text = $"CRC/Oversize: {_parser.CrcErrorCount}/{_parser.OversizePayloadCount}";
            });
        }
        catch (Exception ex)
        {
            OnTransportError(ex.Message);
        }
    }

    private void HandleFrame(ModelFrame frame)
    {
        // Keep UI command flow deterministic by dispatching each protocol command explicitly.
        switch (frame.Cmd)
        {
            case CommandId.Ack:
                HandleAckFrame(frame);
                break;
            case CommandId.GetVarTable:
                HandleVarTableFrame(frame.Payload);
                break;
            case CommandId.ReadMemBatch:
                HandleReadMemFrame(frame.Payload);
                break;
            case CommandId.StreamData:
                if (StreamFrameCodec.TryParsePayload(frame.Payload, out var dataFrame))
                {
                    _dataEngine.Append(dataFrame);
                    if (_recordEngine.IsRecording)
                    {
                        _ = _recordEngine.AppendChunkAsync(
                            new RecordChunk(dataFrame.TimestampUs, dataFrame.TimestampUs, frame.Payload),
                            CancellationToken.None);
                    }
                }
                break;
        }
    }

    private void HandleVarTableFrame(byte[] payload)
    {
        if (!VarEngine.TryParseVarTablePayload(payload, out var descriptors))
        {
            Log("GET_VAR_TABLE parse failed.");
            return;
        }

        _varEngine.SetDescriptors(descriptors);
        Dispatcher.Invoke(() =>
        {
            _variables.Clear();
            _variableMap.Clear();
            foreach (var descriptor in descriptors)
            {
                var item = new VariableItem(descriptor.Name, descriptor.Address, descriptor.Type, descriptor.Scale, descriptor.Unit, 0);
                _variables.Add(item);
                _variableMap[descriptor.Address] = item;
            }

            BottomStatusText.Text = $"Variable table loaded: {descriptors.Count}";
        });
        Log($"GET_VAR_TABLE loaded {descriptors.Count} vars");
    }

    private void HandleReadMemFrame(byte[] payload)
    {
        if (!VarEngine.TryParseReadMemPayload(payload, out var values))
        {
            Log("READ_MEM payload parse failed.");
            return;
        }

        foreach (var (address, raw) in values)
        {
            _varEngine.UpdateValue(address, raw);
            if (!_varEngine.TryGetScaledDouble(address, out var decoded))
            {
                if (raw.Length >= 4) decoded = BitConverter.ToSingle(raw);
                else continue;
            }

            Dispatcher.Invoke(() =>
            {
                if (_variableMap.TryGetValue(address, out var item)) item.Value = decoded;
            });
        }

        Log($"READ_MEM updated {values.Count} item(s)");
    }

    private ushort ParseWaveChannelOrDefault()
    {
        return ushort.TryParse(WaveChannelTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var channel)
            ? channel
            : (ushort)0;
    }

    private double GetReplaySpeed()
    {
        var text = (ReplaySpeedComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.0x";
        text = text.Replace("x", string.Empty, StringComparison.OrdinalIgnoreCase);
        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var speed)
            ? Math.Clamp(speed, 0.1, 32.0)
            : 1.0;
    }

    private void ResetReplayAnchor()
    {
        if (_replayIndex >= _replayFrames.Count) return;
        _replayAnchorTimestampUs = _replayFrames[_replayIndex].TimestampUs;
        _replayAnchorWallClock = DateTime.UtcNow;
    }

    private void PumpReplayFrames()
    {
        if (_replayFrames.Count == 0 || _replayIndex >= _replayFrames.Count)
        {
            _isReplayPlaying = false;
            return;
        }

        if (_replayAnchorWallClock == DateTime.MinValue) ResetReplayAnchor();
        var elapsedUs = Math.Max(0.0, (DateTime.UtcNow - _replayAnchorWallClock).TotalMilliseconds * 1000.0 * GetReplaySpeed());
        var targetTimestampUs = _replayAnchorTimestampUs + (ulong)elapsedUs;

        while (_replayIndex < _replayFrames.Count && _replayFrames[_replayIndex].TimestampUs <= targetTimestampUs)
        {
            OnDataFrameReady(_replayFrames[_replayIndex]);
            _replayIndex++;
        }

        if (_replayIndex < _replayFrames.Count) return;
        _isReplayPlaying = false;
        BottomStatusText.Text = $"Replay complete ({_replayFrames.Count} frames).";
        Log("Replay complete.");
    }

    private void OnDataFrameReady(DataFrame frame)
    {
        var targetChannel = ParseWaveChannelOrDefault();
        var sampleFound = false;
        var sampleValue = 0.0;
        foreach (var channel in frame.Channels)
        {
            if (channel.ChannelId != targetChannel) continue;
            sampleValue = channel.Value;
            sampleFound = true;
            break;
        }

        if (!sampleFound && frame.Channels.Count > 0 && targetChannel == 0)
        {
            sampleValue = frame.Channels[0].Value;
            sampleFound = true;
        }

        if (!sampleFound) return;

        lock (_sampleSync)
        {
            _sampleQueue.Enqueue(sampleValue);
            _sampleCount++;
        }

        Dispatcher.Invoke(() => SamplesStatusText.Text = $"Samples: {_sampleCount}");
    }

    private void OnTransportError(string message)
    {
        Interlocked.Increment(ref _errorCount);
        Dispatcher.Invoke(() =>
        {
            ErrorsStatusText.Text = $"Errors: {_errorCount}";
            BottomStatusText.Text = $"Error: {message}";
            Log($"ERROR {message}");
        });
    }

    private async void OnTransportStateChanged(ConnectionState state)
    {
        Dispatcher.Invoke(() =>
        {
            ConnectionStatusText.Text = $"Status: {state}";
            Log($"State -> {state}");
        });

        if (state == ConnectionState.Error && !_manualDisconnect && _lastConfig is not null)
        {
            // Single delayed retry is enough for MVP and avoids reconnect storms.
            await Task.Delay(1200);
            if (!_transport.IsOpen)
            {
                var ok = await _transport.OpenAsync(_lastConfig, CancellationToken.None);
                Dispatcher.Invoke(() => BottomStatusText.Text = ok ? "Auto reconnect success." : "Auto reconnect failed.");
            }
        }
    }

    private void PlotTimerOnTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isReplayPlaying) PumpReplayFrames();

        if (!_transport.IsOpen && !_isReplayPlaying)
        {
            var t = DateTime.Now.TimeOfDay.TotalSeconds;
            _sampleQueue.Enqueue(Math.Sin(t * 2.0) * 0.7 + Math.Cos(t * 0.6) * 0.2);
            _sampleCount++;
        }

        while (_sampleQueue.TryDequeue(out var sample))
        {
            for (var i = 0; i < _waveBuffer.Length - 1; i++) _waveBuffer[i] = _waveBuffer[i + 1];
            _waveBuffer[^1] = sample;
        }

        RenderWave();
        if (DateTime.UtcNow - _lastStatsUpdate >= TimeSpan.FromSeconds(1))
        {
            _lastStatsUpdate = DateTime.UtcNow;
            var waveChannel = ParseWaveChannelOrDefault();
            if (_dataEngine.TryGetChannelStats(waveChannel, 2000, out var stats))
            {
                BottomStatusText.Text = $"Ch{waveChannel} Mean={stats.Mean:F3}  PkPk={stats.PeakToPeak:F3}  Freq={stats.FrequencyHz:F2}Hz";
            }
        }
    }

    private void RenderWave()
    {
        var width = WaveCanvas.ActualWidth;
        var height = WaveCanvas.ActualHeight;
        if (width <= 1 || height <= 1) return;

        var points = new PointCollection(_waveBuffer.Length);
        var xStep = width / (_waveBuffer.Length - 1);
        for (var i = 0; i < _waveBuffer.Length; i++)
        {
            var x = i * xStep;
            var y = (height * 0.5) - Math.Clamp(_waveBuffer[i], -1.2, 1.2) * (height * 0.35);
            points.Add(new Point(x, y));
        }

        WavePolyline.Points = points;
        WavePolyline.Stroke = _transport.IsOpen
            ? new SolidColorBrush(Color.FromRgb(77, 224, 168))
            : new SolidColorBrush(Color.FromRgb(95, 152, 223));
    }

    private void LoadMockVariables()
    {
        var mock = new[]
        {
            new VariableDescriptor("g_motor_speed", 0x20001000, DataType.Float32, 1, 1.0, "rpm"),
            new VariableDescriptor("g_bus_voltage", 0x20001004, DataType.Float32, 1, 1.0, "V"),
            new VariableDescriptor("g_temp", 0x20001008, DataType.Float32, 1, 1.0, "C")
        };

        _varEngine.SetDescriptors(mock);
        _variables.Clear();
        _variableMap.Clear();
        foreach (var descriptor in mock)
        {
            var item = new VariableItem(descriptor.Name, descriptor.Address, descriptor.Type, descriptor.Scale, descriptor.Unit, 0);
            _variables.Add(item);
            _variableMap[descriptor.Address] = item;
        }
    }

    private int ParseBaudOrDefault()
    {
        var selected = (BaudComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return int.TryParse(selected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var baud) ? baud : 921600;
    }

    private void HandleAckFrame(ModelFrame frame)
    {
        if (frame.Payload.Length >= 4)
        {
            var status = frame.Payload[0];
            var cmd = (CommandId)frame.Payload[1];
            var seq = BitConverter.ToUInt16(frame.Payload, 2);
            var statusText = status switch
            {
                0 => "OK",
                1 => "INVALID_CMD",
                2 => "INVALID_PAYLOAD",
                3 => "BUSY",
                4 => "DENIED",
                _ => $"ERR_{status}"
            };
            Log($"ACK seq={frame.Seq} status={statusText} for={cmd}#{seq}");
            return;
        }

        Log($"ACK seq={frame.Seq}");
    }

    private static bool TryEncodeWriteValue(VariableItem item, out byte[] raw)
    {
        raw = Array.Empty<byte>();
        var scaled = item.Scale == 0 ? item.Value : item.Value / item.Scale;
        switch (item.Type)
        {
            case DataType.Int8:
                raw = new[] { unchecked((byte)(sbyte)Math.Round(scaled)) };
                return true;
            case DataType.UInt8:
                raw = new[] { (byte)Math.Clamp(Math.Round(scaled), (double)byte.MinValue, byte.MaxValue) };
                return true;
            case DataType.Int16:
                raw = BitConverter.GetBytes((short)Math.Clamp(Math.Round(scaled), short.MinValue, short.MaxValue));
                return true;
            case DataType.UInt16:
                raw = BitConverter.GetBytes((ushort)Math.Clamp(Math.Round(scaled), (double)ushort.MinValue, ushort.MaxValue));
                return true;
            case DataType.Int32:
                raw = BitConverter.GetBytes((int)Math.Clamp(Math.Round(scaled), int.MinValue, int.MaxValue));
                return true;
            case DataType.UInt32:
                raw = BitConverter.GetBytes((uint)Math.Clamp(Math.Round(scaled), (double)uint.MinValue, uint.MaxValue));
                return true;
            case DataType.Float64:
                raw = BitConverter.GetBytes(scaled);
                return true;
            default:
                raw = BitConverter.GetBytes((float)scaled);
                return true;
        }
    }

    private static ushort GetDataTypeSize(DataType type)
    {
        return type switch
        {
            DataType.Int8 => 1,
            DataType.UInt8 => 1,
            DataType.Int16 => 2,
            DataType.UInt16 => 2,
            DataType.Int32 => 4,
            DataType.UInt32 => 4,
            DataType.Float64 => 8,
            _ => 4
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _plotTimer.Stop();
        _dataEngine.DataFrameReady -= OnDataFrameReady;
        _transport.DataReceived -= OnTransportData;
        _transport.ErrorOccurred -= OnTransportError;
        _transport.StateChanged -= OnTransportStateChanged;
        _ = _recordEngine.CloseAsync();
        _ = _transport.CloseAsync();
        _transport.Dispose();
        base.OnClosed(e);
    }

    private void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logs.Add(entry);
        while (_logs.Count > 400) _logs.RemoveAt(0);
        if (LogListBox is not null) LogListBox.ScrollIntoView(entry);
    }
}

public sealed class VariableItem : INotifyPropertyChanged
{
    private double _value;

    public VariableItem(string name, uint address, DataType type, double scale, string unit, double value)
    {
        Name = name;
        Address = address;
        Type = type;
        Scale = scale;
        Unit = unit;
        _value = value;
    }

    public string Name { get; }
    public uint Address { get; }
    public DataType Type { get; }
    public string TypeName => Type.ToString();
    public double Scale { get; }
    public string Unit { get; }
    public string AddressHex => $"0x{Address:X8}";

    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) < 1e-12) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
