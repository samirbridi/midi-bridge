using System.Threading.Channels;
using Bridge.Core.Midi;

namespace Bridge.Core.Routing;

public sealed class MidiPipe : IMidiMessageSource, IMidiMessageSink
{
    private readonly Channel<Midi1Message> _channel;

    public MidiPipe(int capacity = 8192)
    {
        var opts = new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _channel = Channel.CreateBounded<Midi1Message>(opts);
    }

    public ValueTask WriteAsync(Midi1Message message, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<Midi1Message> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

