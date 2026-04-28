using System.Runtime.InteropServices;
using System.Threading.Channels;
using Bridge.Core.Midi;
using Bridge.Core.Routing;

namespace Bridge.IO.Midi1.WinMM;

public sealed class WinMmMidiInput : IMidiMessageSource, IAsyncDisposable
{
    private const uint CALLBACK_FUNCTION = 0x00030000;
    private const uint MIM_DATA = 0x3C3;

    private readonly Channel<Midi1Message> _channel = Channel.CreateUnbounded<Midi1Message>();
    private readonly WinMmNative.MidiInProc _callback;
    private nint _handle;

    public WinMmMidiInput(int deviceId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("WinMM requires Windows");
        }

        _callback = Callback;
        var res = WinMmNative.midiInOpen(out _handle, (nuint)deviceId, _callback, 0, CALLBACK_FUNCTION);
        if (res != 0 || _handle == 0)
        {
            throw new InvalidOperationException($"midiInOpen failed. Result={res}");
        }

        res = WinMmNative.midiInStart(_handle);
        if (res != 0)
        {
            throw new InvalidOperationException($"midiInStart failed. Result={res}");
        }
    }

    public IAsyncEnumerable<Midi1Message> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    private void Callback(nint hMidiIn, uint wMsg, nuint dwInstance, nuint dwParam1, nuint dwParam2)
    {
        if (wMsg != MIM_DATA)
        {
            return;
        }

        var packed = (uint)dwParam1;
        var status = (byte)(packed & 0xFF);
        var data1 = (byte)((packed >> 8) & 0xFF);
        var data2 = (byte)((packed >> 16) & 0xFF);

        byte[] msg = status switch
        {
            >= 0xC0 and <= 0xDF => new[] { status, data1 },
            _ => new[] { status, data1, data2 }
        };

        _channel.Writer.TryWrite(Midi1Message.FromArray(DateTimeOffset.UtcNow, msg));
    }

    public ValueTask DisposeAsync()
    {
        if (_handle != 0)
        {
            WinMmNative.midiInStop(_handle);
            WinMmNative.midiInClose(_handle);
            _handle = 0;
        }

        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

