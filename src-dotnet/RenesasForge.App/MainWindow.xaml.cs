using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
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

    private readonly ObservableCollection<VariableItem> _variables = new();
    private readonly Dictionary<uint, VariableItem> _variableMap = new();
    private readonly ObservableCollection<string> _logs = new();

    private TransportConfig? _lastConfig;
    private bool _manualDisconnect;
    private DateTime _lastStatsUpdate = DateTime.MinValue;

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
        _lastConfig = new TransportConfig(selected, 921600);
        var ok = await _transport.OpenAsync(_lastConfig, CancellationToken.None);
        BottomStatusText.Text = ok ? $"Connected to {selected}" : $"Failed to connect to {selected}";
        Log(ok ? $"Connected {selected}" : $"Connect failed {selected}");
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

    private async void StartRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var path = System.IO.Path.Combine(Environment.CurrentDirectory, $"record_{DateTime.Now:yyyyMMdd_HHmmss}.rfr");
        await _recordEngine.StartAsync(path, CancellationToken.None);
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
        var path = System.IO.Path.Combine(Environment.CurrentDirectory, $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await _recordEngine.ExportCsvAsync(path, _dataEngine.RecentFrames(int.MaxValue), CancellationToken.None);
        BottomStatusText.Text = $"CSV exported: {path}";
        Log($"CSV exported: {path}");
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
        if (VariableGrid.SelectedItem is not VariableItem item)
        {
            BottomStatusText.Text = "Select a variable first.";
            return;
        }

        var payload = new byte[8];
        BitConverter.GetBytes(item.Address).CopyTo(payload, 0);
        var f32 = (float)item.Value;
        BitConverter.GetBytes(f32).CopyTo(payload, 4);

        await SendCommandAsync(CommandId.WriteMem, payload);
        Log($"WRITE_MEM {item.Name}={item.Value.ToString(CultureInfo.InvariantCulture)}");
    }

    private async void ReadSelectedVarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (VariableGrid.SelectedItem is not VariableItem item)
        {
            BottomStatusText.Text = "Select a variable first.";
            return;
        }

        var payload = new byte[6];
        BitConverter.GetBytes(item.Address).CopyTo(payload, 0);
        BitConverter.GetBytes((ushort)4).CopyTo(payload, 4);
        await SendCommandAsync(CommandId.ReadMemBatch, payload);
        Log($"READ_MEM_BATCH {item.Name} @ {item.AddressHex}");
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
            });
        }
        catch (Exception ex)
        {
            OnTransportError(ex.Message);
        }
    }

    private void HandleFrame(ModelFrame frame)
    {
        switch (frame.Cmd)
        {
            case CommandId.Ack:
                Log($"ACK seq={frame.Seq}");
                break;
            case CommandId.GetVarTable:
                HandleVarTableFrame(frame.Payload);
                break;
            case CommandId.ReadMemBatch:
                HandleReadMemFrame(frame.Payload);
                break;
            case CommandId.StreamData:
                if (TryParseDataFrame(frame, out var dataFrame))
                {
                    _dataEngine.Append(dataFrame);
                    if (_recordEngine.IsRecording)
                    {
                        var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000UL;
                        _ = _recordEngine.AppendChunkAsync(new RecordChunk(now, now, frame.Payload), CancellationToken.None);
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
                var item = new VariableItem(descriptor.Name, descriptor.Address, 0);
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

    private bool TryParseDataFrame(ModelFrame frame, out DataFrame dataFrame)
    {
        dataFrame = new DataFrame(0, Array.Empty<ChannelValue>());
        if (frame.Cmd != CommandId.StreamData || frame.Payload.Length == 0) return false;

        if (frame.Payload.Length >= 8 && (frame.Payload.Length - 8) % 6 == 0)
        {
            var ts = BitConverter.ToUInt64(frame.Payload, 0);
            var list = new List<ChannelValue>();
            for (var i = 8; i + 5 < frame.Payload.Length; i += 6)
            {
                var ch = BitConverter.ToUInt16(frame.Payload, i);
                var val = BitConverter.ToSingle(frame.Payload, i + 2);
                list.Add(new ChannelValue(ch, val));
            }

            dataFrame = new DataFrame(ts, list);
            return list.Count > 0;
        }

        var text = Encoding.ASCII.GetString(frame.Payload);
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var channels = new List<ChannelValue>();
        ushort chId = 0;
        foreach (var part in parts)
        {
            if (double.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                channels.Add(new ChannelValue(chId++, parsed));
            }
        }

        if (channels.Count == 0) return false;
        var tsUs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000UL;
        dataFrame = new DataFrame(tsUs, channels);
        return true;
    }

    private void OnDataFrameReady(DataFrame frame)
    {
        lock (_sampleSync)
        {
            foreach (var ch in frame.Channels)
            {
                _sampleQueue.Enqueue(ch.Value);
                _sampleCount++;
            }
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

        if (!_transport.IsOpen)
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
            if (_dataEngine.TryGetChannelStats(0, 2000, out var stats))
            {
                BottomStatusText.Text = $"Ch0 Mean={stats.Mean:F3}  PkPk={stats.PeakToPeak:F3}  Freq={stats.FrequencyHz:F2}Hz";
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
            var item = new VariableItem(descriptor.Name, descriptor.Address, 0);
            _variables.Add(item);
            _variableMap[descriptor.Address] = item;
        }
    }

    private void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logs.Add(entry);
        while (_logs.Count > 400) _logs.RemoveAt(0);
        LogListBox.ScrollIntoView(entry);
    }
}

public sealed class VariableItem : INotifyPropertyChanged
{
    private double _value;

    public VariableItem(string name, uint address, double value)
    {
        Name = name;
        Address = address;
        _value = value;
    }

    public string Name { get; }
    public uint Address { get; }
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
