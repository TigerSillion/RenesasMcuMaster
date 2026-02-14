using System.Text;
using FluentAssertions;
using RenesasForge.Core;
using RenesasForge.Core.Models;
using RenesasForge.Protocol;
using RenesasForge.Protocol.Parsers;

namespace RenesasForge.Tests.Unit;

public sealed class ParserAndCoreTests
{
    [Fact]
    public void RForgeBinaryParser_ShouldParsePing()
    {
        var parser = new RForgeBinaryParser();
        var payload = new byte[] { 0x41, 0x42, 0x43 };
        var packet = parser.BuildCommand(CommandId.Ping, payload);
        parser.Feed(packet);

        parser.TryPopFrame(out var frame).Should().BeTrue();
        frame.Cmd.Should().Be(CommandId.Ping);
        frame.Payload.Should().Equal(payload);
    }

    [Fact]
    public void RForgeBinaryParser_ShouldRecoverAfterBadCrc_ThenParseValid()
    {
        var parser = new RForgeBinaryParser();
        var badPacket = parser.BuildCommand(CommandId.Ping, new byte[] { 0x01 });
        badPacket[^1] ^= 0xFF;
        var goodPacket = parser.BuildCommand(CommandId.Ack, new byte[] { 0xA1, 0xB2 });

        parser.Feed(badPacket.Concat(goodPacket).ToArray());

        parser.CrcErrorCount.Should().BeGreaterThan(0);
        parser.TryPopFrame(out var frame).Should().BeTrue();
        frame.Cmd.Should().Be(CommandId.Ack);
        frame.Payload.Should().Equal(new byte[] { 0xA1, 0xB2 });
    }

    [Fact]
    public void RForgeBinaryParser_ShouldHandleHalfPacket()
    {
        var parser = new RForgeBinaryParser();
        var packet = parser.BuildCommand(CommandId.StreamStart, new byte[] { 0x01, 0x02, 0x03 });

        parser.Feed(packet.AsSpan(0, 4));
        parser.TryPopFrame(out _).Should().BeFalse();

        parser.Feed(packet.AsSpan(4));
        parser.TryPopFrame(out var frame).Should().BeTrue();
        frame.Cmd.Should().Be(CommandId.StreamStart);
    }

    [Fact]
    public void ParserManager_AutoDetect_ShouldSwitchToBinary()
    {
        var manager = new ParserManager();
        var parser = new RForgeBinaryParser();
        var packet = parser.BuildCommand(CommandId.Ping, Array.Empty<byte>());

        manager.Feed(packet);
        manager.TryPopFrame(out var frame).Should().BeTrue();
        frame.Cmd.Should().Be(CommandId.Ping);
        manager.ActiveType.Should().Be(ParserType.RForgeBinary);
    }

    [Fact]
    public void ParserManager_AutoDetect_ShouldSwitchToVofa()
    {
        var manager = new ParserManager();
        var bytes = Encoding.ASCII.GetBytes("1.0,2.0,3.0\n");

        manager.Feed(bytes);
        manager.TryPopFrame(out var frame).Should().BeTrue();
        frame.Cmd.Should().Be(CommandId.StreamData);
        manager.ActiveType.Should().Be(ParserType.VofaCompatible);
    }

    [Fact]
    public void VarEngine_ShouldParseTextVarTableAndReadMem()
    {
        var tablePayload = Encoding.ASCII.GetBytes("g_speed,0x20001000,float32,1.0,rpm;g_temp,0x20001004,float32,1.0,C");
        VarEngine.TryParseVarTablePayload(tablePayload, out var descriptors).Should().BeTrue();
        descriptors.Count.Should().Be(2);

        var engine = new VarEngine();
        engine.SetDescriptors(descriptors);
        var readPayload = Encoding.ASCII.GetBytes("0x20001000=123.5,0x20001004=45.25");
        VarEngine.TryParseReadMemPayload(readPayload, out var values).Should().BeTrue();
        foreach (var (address, raw) in values) engine.UpdateValue(address, raw);

        engine.TryGetScaledDouble(0x20001000, out var v1).Should().BeTrue();
        engine.TryGetScaledDouble(0x20001004, out var v2).Should().BeTrue();
        v1.Should().BeApproximately(123.5, 0.0001);
        v2.Should().BeApproximately(45.25, 0.0001);
    }

    [Fact]
    public async Task RecordEngine_ShouldWriteAndReadChunks()
    {
        var engine = new RecordEngine();
        var path = Path.Combine(Path.GetTempPath(), $"rf_test_{Guid.NewGuid():N}.rfr");
        try
        {
            await engine.StartAsync(path, CancellationToken.None);
            await engine.AppendChunkAsync(new RecordChunk(10, 20, new byte[] { 1, 2, 3 }), CancellationToken.None);
            await engine.AppendChunkAsync(new RecordChunk(21, 30, new byte[] { 4, 5 }), CancellationToken.None);
            await engine.CloseAsync();

            var chunks = new List<RecordChunk>();
            await foreach (var chunk in engine.ReadChunksAsync(path))
            {
                chunks.Add(chunk);
            }

            chunks.Should().HaveCount(2);
            chunks[0].StartTs.Should().Be(10);
            chunks[0].PackedSamples.Should().Equal(new byte[] { 1, 2, 3 });
            chunks[1].PackedSamples.Should().Equal(new byte[] { 4, 5 });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
