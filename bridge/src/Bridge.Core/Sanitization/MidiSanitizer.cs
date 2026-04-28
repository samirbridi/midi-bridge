using Bridge.Core.Midi;

namespace Bridge.Core.Sanitization;

public sealed class MidiSanitizer
{
    private readonly MidiSanitizerOptions _options;
    private readonly object _gate = new();
    private readonly Dictionary<int, (DateTimeOffset At, byte[] Data)> _lastCc = new();
    private DateTimeOffset _windowStart = DateTimeOffset.MinValue;
    private int _windowCount;

    public MidiSanitizer(MidiSanitizerOptions? options = null)
    {
        _options = options ?? MidiSanitizerOptions.Default;
    }

    public bool ShouldDrop(Midi1Message message)
    {
        lock (_gate)
        {
            var now = message.Timestamp;
            if (_windowStart == DateTimeOffset.MinValue || now - _windowStart >= TimeSpan.FromSeconds(1))
            {
                _windowStart = now;
                _windowCount = 0;
            }

            _windowCount++;
            if (_options.MaxMessagesPerSecondPerRoute > 0 && _windowCount > _options.MaxMessagesPerSecondPerRoute)
            {
                return true;
            }

            if (TryCoalesceControlChange(message, now, out var shouldDrop))
            {
                return shouldDrop;
            }

            return false;
        }
    }

    private bool TryCoalesceControlChange(Midi1Message message, DateTimeOffset now, out bool shouldDrop)
    {
        shouldDrop = false;
        var span = message.Data.Span;
        if (span.Length != 3)
        {
            return false;
        }

        var status = span[0];
        if ((status & 0xF0) != 0xB0)
        {
            return false;
        }

        var channel = status & 0x0F;
        var controller = span[1];
        var key = (channel << 8) | controller;

        if (_lastCc.TryGetValue(key, out var last))
        {
            if (now - last.At <= _options.CoalesceWindow)
            {
                if (last.Data.Length == 3 && last.Data[2] == span[2])
                {
                    shouldDrop = true;
                    return true;
                }
            }
        }

        _lastCc[key] = (now, message.ToArray());
        return true;
    }
}

