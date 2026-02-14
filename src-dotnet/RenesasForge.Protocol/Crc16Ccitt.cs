namespace RenesasForge.Protocol;

public static class Crc16Ccitt
{
    public static ushort Compute(ReadOnlySpan<byte> data, ushort init = 0xFFFF)
    {
        ushort crc = init;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (ushort)(((crc & 0x8000) != 0) ? ((crc << 1) ^ 0x1021) : (crc << 1));
        }
        return crc;
    }
}
