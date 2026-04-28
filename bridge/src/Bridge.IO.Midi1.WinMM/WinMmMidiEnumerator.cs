using System.Runtime.InteropServices;

namespace Bridge.IO.Midi1.WinMM;

public sealed class WinMmMidiEnumerator
{
    public IReadOnlyList<WinMmMidiDeviceInfo> ListInputs()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Array.Empty<WinMmMidiDeviceInfo>();
        }

        var count = (int)WinMmNative.midiInGetNumDevs();
        var list = new List<WinMmMidiDeviceInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var res = WinMmNative.midiInGetDevCapsW((nuint)i, out var caps, (uint)Marshal.SizeOf<MidiInCaps>());
            if (res != 0)
            {
                continue;
            }

            list.Add(new WinMmMidiDeviceInfo(i, caps.szPname));
        }

        return list;
    }

    public IReadOnlyList<WinMmMidiDeviceInfo> ListOutputs()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Array.Empty<WinMmMidiDeviceInfo>();
        }

        var count = (int)WinMmNative.midiOutGetNumDevs();
        var list = new List<WinMmMidiDeviceInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var res = WinMmNative.midiOutGetDevCapsW((nuint)i, out var caps, (uint)Marshal.SizeOf<MidiOutCaps>());
            if (res != 0)
            {
                continue;
            }

            list.Add(new WinMmMidiDeviceInfo(i, caps.szPname));
        }

        return list;
    }
}
