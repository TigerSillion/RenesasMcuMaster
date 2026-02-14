using RenesasForge.Core.Models;
using RenesasForge.Protocol.Parsers;

namespace RenesasForge.Protocol;

public sealed class ParserManager
{
    private readonly RForgeBinaryParser _rforge = new();
    private readonly VofaCompatibleParser _vofa = new();
    private ParserType? _autoDetectedType;

    public ParserType Mode { get; private set; } = ParserType.AutoDetect;
    public ParserType ActiveType => Mode == ParserType.AutoDetect ? _autoDetectedType ?? ParserType.AutoDetect : Mode;

    public void SetMode(ParserType mode)
    {
        Mode = mode;
        _autoDetectedType = null;
        _rforge.Reset();
        _vofa.Reset();
    }

    public void Feed(ReadOnlySpan<byte> bytes)
    {
        if (Mode == ParserType.RForgeBinary)
        {
            _rforge.Feed(bytes);
            return;
        }

        if (Mode == ParserType.VofaCompatible)
        {
            _vofa.Feed(bytes);
            return;
        }

        _rforge.Feed(bytes);
        _vofa.Feed(bytes);
    }

    public bool TryPopFrame(out Frame frame)
    {
        frame = new Frame(CommandId.Unknown, 0, Array.Empty<byte>());

        if (Mode == ParserType.RForgeBinary) return _rforge.TryPopFrame(out frame);
        if (Mode == ParserType.VofaCompatible) return _vofa.TryPopFrame(out frame);

        if (_autoDetectedType == ParserType.RForgeBinary && _rforge.TryPopFrame(out frame)) return true;
        if (_autoDetectedType == ParserType.VofaCompatible && _vofa.TryPopFrame(out frame)) return true;

        if (_rforge.TryPopFrame(out frame))
        {
            _autoDetectedType = ParserType.RForgeBinary;
            return true;
        }

        if (_vofa.TryPopFrame(out frame))
        {
            _autoDetectedType = ParserType.VofaCompatible;
            return true;
        }

        return false;
    }

    public byte[] BuildCommand(CommandId cmd, ReadOnlySpan<byte> payload)
    {
        var active = Mode switch
        {
            ParserType.RForgeBinary => ParserType.RForgeBinary,
            ParserType.VofaCompatible => ParserType.VofaCompatible,
            _ => _autoDetectedType ?? ParserType.RForgeBinary
        };

        return active == ParserType.VofaCompatible
            ? _vofa.BuildCommand(cmd, payload)
            : _rforge.BuildCommand(cmd, payload);
    }
}
