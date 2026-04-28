using Bridge.Core.Midi;

namespace Bridge.Core.Led;

public sealed class LedStateCache
{
    private readonly object _gate = new();
    private readonly Dictionary<int, byte[]> _states = new();

    public void Clear()
    {
        lock (_gate)
        {
            _states.Clear();
        }
    }

    public bool TryApply(Midi1Message message, out int key)
    {
        key = 0;
        var span = message.Data.Span;
        if (span.Length != 3)
        {
            return false;
        }

        var status = span[0];
        var type = status & 0xF0;
        if (type is not (0x80 or 0x90 or 0xB0))
        {
            return false;
        }

        var channel = status & 0x0F;
        var number = span[1];
        key = (type << 16) | (channel << 8) | number;

        lock (_gate)
        {
            _states[key] = message.ToArray();
        }

        return true;
    }

    public IReadOnlyList<byte[]> Snapshot()
    {
        lock (_gate)
        {
            return _states.Values.Select(v => (byte[])v.Clone()).ToArray();
        }
    }
}

