using Bridge.Core.Midi;
using Bridge.Core.Sanitization;

namespace Bridge.Core.Routing;

public sealed class MidiRouter
{
    private readonly MidiSanitizer _sanitizer;

    public MidiRouter(MidiSanitizer? sanitizer = null)
    {
        _sanitizer = sanitizer ?? new MidiSanitizer();
    }

    public async Task RunAsync(
        IMidiMessageSource source,
        IMidiMessageSink sink,
        CancellationToken cancellationToken = default)
    {
        await foreach (var msg in source.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_sanitizer.ShouldDrop(msg))
            {
                continue;
            }

            await sink.WriteAsync(msg, cancellationToken).ConfigureAwait(false);
        }
    }
}

