using Bridge.Core.Midi;

namespace Bridge.Core.Routing;

public interface IMidiMessageSource
{
    IAsyncEnumerable<Midi1Message> ReadAllAsync(CancellationToken cancellationToken = default);
}

