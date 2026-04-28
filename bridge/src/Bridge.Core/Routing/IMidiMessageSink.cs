using Bridge.Core.Midi;

namespace Bridge.Core.Routing;

public interface IMidiMessageSink
{
    ValueTask WriteAsync(Midi1Message message, CancellationToken cancellationToken = default);
}

