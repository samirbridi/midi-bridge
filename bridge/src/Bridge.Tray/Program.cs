using System.Runtime.InteropServices;

Console.WriteLine("USB-MIDI Bridge (Tray placeholder)");
if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Console.WriteLine("Non-Windows OS detected. Tray UI is disabled.");
}

Console.WriteLine("Next steps: implement tray UI + IPC to Bridge.Service.");
