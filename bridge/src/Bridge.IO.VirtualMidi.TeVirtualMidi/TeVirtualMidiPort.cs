using System.Runtime.InteropServices;
using Bridge.Core.Midi;
using Bridge.Core.Routing;

namespace Bridge.IO.VirtualMidi.TeVirtualMidi;

public sealed class TeVirtualMidiPort : IMidiMessageSource, IMidiMessageSink, IAsyncDisposable
{
    private readonly nint _handle;
    private readonly int _maxPacketSize;

    public TeVirtualMidiPort(string name, int maxSysexLength = 65535, int maxPacketSize = 4096)
    {
        _maxPacketSize = maxPacketSize;
        var manufacturer = Guid.Empty;
        var product = Guid.Empty;
        var driver = Guid.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("teVirtualMIDI requires Windows");
        }

        _handle = TeVirtualMidiNative.virtualMIDICreatePortEx2(
            name,
            IntPtr.Zero,
            IntPtr.Zero,
            (uint)maxSysexLength,
            0,
            ref manufacturer,
            ref product,
            ref driver
        );

        if (_handle == 0)
        {
            throw new InvalidOperationException($"Failed to create teVirtualMIDI port '{name}'. Win32Error={Marshal.GetLastWin32Error()}");
        }
    }

    public async ValueTask WriteAsync(Midi1Message message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = message.ToArray();
        if (!TeVirtualMidiNative.virtualMIDISendData(_handle, bytes, (uint)bytes.Length))
        {
            throw new InvalidOperationException($"virtualMIDISendData failed. Win32Error={Marshal.GetLastWin32Error()}");
        }

        await ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Midi1Message> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new byte[_maxPacketSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            uint len = (uint)buffer.Length;
            var ok = TeVirtualMidiNative.virtualMIDIGetData(_handle, buffer, ref len);
            if (!ok)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (len == 0)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var packet = new byte[len];
            Buffer.BlockCopy(buffer, 0, packet, 0, (int)len);
            yield return Midi1Message.FromArray(DateTimeOffset.UtcNow, packet);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_handle != 0)
        {
            TeVirtualMidiNative.virtualMIDIShutdown(_handle);
        }

        return ValueTask.CompletedTask;
    }
}

