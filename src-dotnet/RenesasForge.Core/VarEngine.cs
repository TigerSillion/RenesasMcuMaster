using System.Globalization;
using System.Text;
using RenesasForge.Core.Models;

namespace RenesasForge.Core;

public sealed class VarEngine
{
    private readonly Dictionary<uint, byte[]> _rawValues = new();
    private readonly object _sync = new();

    public IReadOnlyList<VariableDescriptor> Descriptors { get; private set; } = Array.Empty<VariableDescriptor>();

    public void SetDescriptors(IReadOnlyList<VariableDescriptor> descriptors)
    {
        Descriptors = descriptors;
    }

    public void UpdateValue(uint address, byte[] raw)
    {
        lock (_sync)
        {
            _rawValues[address] = raw;
        }
    }

    public byte[]? GetRaw(uint address)
    {
        lock (_sync)
        {
            return _rawValues.TryGetValue(address, out var raw) ? raw : null;
        }
    }

    public bool TryGetScaledDouble(uint address, out double value)
    {
        value = 0;
        var descriptor = Descriptors.FirstOrDefault(x => x.Address == address);
        if (descriptor is null) return false;

        var raw = GetRaw(address);
        if (raw is null || raw.Length == 0) return false;

        if (!TryDecodeRaw(descriptor.Type, raw, out var decoded)) return false;
        value = decoded * descriptor.Scale;
        return true;
    }

    public static bool TryParseVarTablePayload(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<VariableDescriptor> descriptors)
    {
        descriptors = Array.Empty<VariableDescriptor>();
        if (payload.Length == 0) return false;

        if (TryParseVarTableBinary(payload, out descriptors)) return true;
        return TryParseVarTableText(payload, out descriptors);
    }

    public static bool TryParseReadMemPayload(ReadOnlySpan<byte> payload, out IReadOnlyList<(uint Address, byte[] Raw)> values)
    {
        values = Array.Empty<(uint Address, byte[] Raw)>();
        if (payload.Length == 0) return false;

        if (TryParseReadMemBinary(payload, out values)) return true;
        return TryParseReadMemText(payload, out values);
    }

    private static bool TryParseVarTableBinary(ReadOnlySpan<byte> payload, out IReadOnlyList<VariableDescriptor> descriptors)
    {
        descriptors = Array.Empty<VariableDescriptor>();
        if (payload.Length < 2) return false;

        var idx = 0;
        var count = BitConverter.ToUInt16(payload.Slice(idx, 2));
        idx += 2;

        var result = new List<VariableDescriptor>(count);
        for (var i = 0; i < count; i++)
        {
            if (idx + 4 + 1 + 2 + 4 + 1 + 1 > payload.Length) return false;

            var addr = BitConverter.ToUInt32(payload.Slice(idx, 4));
            idx += 4;

            var typeRaw = payload[idx++];
            if (!Enum.IsDefined(typeof(DataType), typeRaw)) return false;
            var type = (DataType)typeRaw;

            var arraySize = BitConverter.ToUInt16(payload.Slice(idx, 2));
            idx += 2;

            var scale = BitConverter.ToSingle(payload.Slice(idx, 4));
            idx += 4;

            var unitLen = payload[idx++];
            var nameLen = payload[idx++];
            if (idx + unitLen + nameLen > payload.Length) return false;

            var unit = Encoding.ASCII.GetString(payload.Slice(idx, unitLen));
            idx += unitLen;

            var name = Encoding.ASCII.GetString(payload.Slice(idx, nameLen));
            idx += nameLen;

            result.Add(new VariableDescriptor(name, addr, type, arraySize, scale, unit));
        }

        descriptors = result;
        return result.Count > 0;
    }

    private static bool TryParseVarTableText(ReadOnlySpan<byte> payload, out IReadOnlyList<VariableDescriptor> descriptors)
    {
        descriptors = Array.Empty<VariableDescriptor>();
        var text = Encoding.ASCII.GetString(payload);
        var lines = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return false;

        var result = new List<VariableDescriptor>(lines.Length);
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 5) continue;

            if (!TryParseAddress(parts[1], out var address)) continue;
            var type = ParseDataType(parts[2]);
            if (!double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var scale)) scale = 1.0;
            var unit = parts[4];
            var arraySize = 1U;
            if (parts.Length >= 6 && uint.TryParse(parts[5], out var parsedArray)) arraySize = parsedArray;

            result.Add(new VariableDescriptor(parts[0], address, type, arraySize, scale, unit));
        }

        if (result.Count == 0) return false;
        descriptors = result;
        return true;
    }

    private static bool TryParseReadMemBinary(ReadOnlySpan<byte> payload, out IReadOnlyList<(uint Address, byte[] Raw)> values)
    {
        values = Array.Empty<(uint Address, byte[] Raw)>();
        if (payload.Length < 2) return false;

        var idx = 0;
        var count = BitConverter.ToUInt16(payload.Slice(idx, 2));
        idx += 2;
        var result = new List<(uint Address, byte[] Raw)>(count);

        for (var i = 0; i < count; i++)
        {
            if (idx + 4 + 2 > payload.Length) return false;
            var addr = BitConverter.ToUInt32(payload.Slice(idx, 4));
            idx += 4;
            var size = BitConverter.ToUInt16(payload.Slice(idx, 2));
            idx += 2;
            if (idx + size > payload.Length) return false;

            var raw = payload.Slice(idx, size).ToArray();
            idx += size;
            result.Add((addr, raw));
        }

        values = result;
        return result.Count > 0;
    }

    private static bool TryParseReadMemText(ReadOnlySpan<byte> payload, out IReadOnlyList<(uint Address, byte[] Raw)> values)
    {
        values = Array.Empty<(uint Address, byte[] Raw)>();
        var text = Encoding.ASCII.GetString(payload);
        var items = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 0) return false;

        var result = new List<(uint Address, byte[] Raw)>();
        foreach (var item in items)
        {
            var pair = item.Split('=', StringSplitOptions.TrimEntries);
            if (pair.Length != 2) continue;

            if (!TryParseAddress(pair[0], out var address)) continue;
            if (!float.TryParse(pair[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) continue;

            result.Add((address, BitConverter.GetBytes(value)));
        }

        values = result;
        return result.Count > 0;
    }

    private static bool TryParseAddress(string text, out uint address)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
    }

    private static DataType ParseDataType(string text)
    {
        return text.ToLowerInvariant() switch
        {
            "int8" => DataType.Int8,
            "uint8" => DataType.UInt8,
            "int16" => DataType.Int16,
            "uint16" => DataType.UInt16,
            "int32" => DataType.Int32,
            "uint32" => DataType.UInt32,
            "float64" => DataType.Float64,
            _ => DataType.Float32
        };
    }

    private static bool TryDecodeRaw(DataType type, ReadOnlySpan<byte> raw, out double value)
    {
        value = 0;
        if (raw.Length == 0) return false;

        switch (type)
        {
            case DataType.Int8:
                value = unchecked((sbyte)raw[0]);
                return true;
            case DataType.UInt8:
                value = raw[0];
                return true;
            case DataType.Int16:
                if (raw.Length < 2) return false;
                value = BitConverter.ToInt16(raw);
                return true;
            case DataType.UInt16:
                if (raw.Length < 2) return false;
                value = BitConverter.ToUInt16(raw);
                return true;
            case DataType.Int32:
                if (raw.Length < 4) return false;
                value = BitConverter.ToInt32(raw);
                return true;
            case DataType.UInt32:
                if (raw.Length < 4) return false;
                value = BitConverter.ToUInt32(raw);
                return true;
            case DataType.Float64:
                if (raw.Length < 8) return false;
                value = BitConverter.ToDouble(raw);
                return true;
            default:
                if (raw.Length < 4) return false;
                value = BitConverter.ToSingle(raw);
                return true;
        }
    }
}
