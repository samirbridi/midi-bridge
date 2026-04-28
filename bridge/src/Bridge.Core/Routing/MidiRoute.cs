namespace Bridge.Core.Routing;

public sealed record MidiRoute(string Name, MidiRouteDirection Direction);

public enum MidiRouteDirection
{
    HardwareToApp,
    AppToHardware,
}

