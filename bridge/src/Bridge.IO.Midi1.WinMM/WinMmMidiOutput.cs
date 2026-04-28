using System.Runtime.InteropServices;
using Bridge.Core.Midi;
using Bridge.Core.Routing;

namespace Bridge.IO.Midi1.WinMM;

public sealed class WinMmMidiOutput : IMidiMessageSink, IAsyncDisposable
{
    private nint _handle;

    public WinMmMidiOutput(int deviceId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("WinMM requires Windows");
        }

        var res = WinMmNative.midiOutOpen(out _handle, (nuint)deviceId, 0, 0, 0);
        if (res != 0 || _handle == 0)
        {
            throw new InvalidOperationException($"midiOutOpen failed. Result={res}");
        }
    }

    public ValueTask WriteAsync(Midi1Message message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var span = message.Data.Span;
        if (span.Length is not (1 or 2 or 3))
        {
            return ValueTask.CompletedTask;
        }

        uint packed = 0;
        if (span.Length >= 1) packed |= span[0];
        if (span.Length >= 2) packed |= (uint)span[1] << 8;
        if (span.Length >= 3) packed |= (uint)span[2] << 16;

        var res = WinMmNative.midiOutShortMsg(_handle, packed);
        if (res != 0)
        {
            throw new InvalidOperationException($"midiOutShortMsg failed. Result={res}");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_handle != 0)
        {
            WinMmNative.midiOutClose(_handle);
            _handle = 0;
        }

        return ValueTask.CompletedTask;
    }
}

