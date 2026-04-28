using System.Runtime.InteropServices;

namespace Bridge.IO.VirtualMidi.TeVirtualMidi;

internal static class TeVirtualMidiNative
{
    [DllImport("teVirtualMIDI", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint virtualMIDICreatePortEx2(
        string portName,
        IntPtr callback,
        IntPtr dwCallbackInstance,
        uint maxSysexLength,
        uint flags,
        ref Guid manufacturer,
        ref Guid product,
        ref Guid driver
    );

    [DllImport("teVirtualMIDI", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool virtualMIDIShutdown(nint midiPort);

    [DllImport("teVirtualMIDI", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool virtualMIDISendData(nint midiPort, byte[] midiDataBytes, uint length);

    [DllImport("teVirtualMIDI", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool virtualMIDIGetData(nint midiPort, byte[] midiDataBytes, ref uint length);
}

