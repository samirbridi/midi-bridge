using System.Runtime.InteropServices;

namespace Bridge.IO.Midi1.WinMM;

internal static class WinMmNative
{
    public const int MAXPNAMELEN = 32;

    [DllImport("winmm.dll")]
    public static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    public static extern uint midiInGetDevCapsW(nuint uDeviceID, out MidiInCaps caps, uint cbMidiInCaps);

    [DllImport("winmm.dll")]
    public static extern uint midiOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    public static extern uint midiOutGetDevCapsW(nuint uDeviceID, out MidiOutCaps caps, uint cbMidiOutCaps);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void MidiInProc(nint hMidiIn, uint wMsg, nuint dwInstance, nuint dwParam1, nuint dwParam2);

    [DllImport("winmm.dll")]
    public static extern uint midiInOpen(out nint lphMidiIn, nuint uDeviceID, MidiInProc dwCallback, nuint dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    public static extern uint midiInStart(nint hMidiIn);

    [DllImport("winmm.dll")]
    public static extern uint midiInStop(nint hMidiIn);

    [DllImport("winmm.dll")]
    public static extern uint midiInClose(nint hMidiIn);

    [DllImport("winmm.dll")]
    public static extern uint midiOutOpen(out nint lphMidiOut, nuint uDeviceID, nuint dwCallback, nuint dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    public static extern uint midiOutShortMsg(nint hMidiOut, uint dwMsg);

    [DllImport("winmm.dll")]
    public static extern uint midiOutClose(nint hMidiOut);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MidiInCaps
{
    public ushort wMid;
    public ushort wPid;
    public uint vDriverVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinMmNative.MAXPNAMELEN)]
    public string szPname;
    public uint dwSupport;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MidiOutCaps
{
    public ushort wMid;
    public ushort wPid;
    public uint vDriverVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinMmNative.MAXPNAMELEN)]
    public string szPname;
    public ushort wTechnology;
    public ushort wVoices;
    public ushort wNotes;
    public ushort wChannelMask;
    public uint dwSupport;
}

