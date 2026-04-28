using Bridge.Core.Midi;

namespace Bridge.Diagnostics;

public sealed class MidiEventRecorder
{
    private readonly object _gate = new();
    private readonly List<RecordedMidiEvent> _events = new();

    public void Record(string route, Midi1Message message, string direction)
    {
        lock (_gate)
        {
            _events.Add(new RecordedMidiEvent(route, direction, message.Timestamp, message.ToArray()));
        }
    }

    public IReadOnlyList<RecordedMidiEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }
}

public sealed record RecordedMidiEvent(
    string Route,
    string Direction,
    DateTimeOffset Timestamp,
    byte[] Data
);

