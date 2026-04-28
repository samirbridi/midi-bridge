namespace Bridge.Core.Profiles;

public sealed record BridgeProfile(
    string Id,
    string DisplayName,
    BridgeProfileMatch Match,
    BridgeProfileLed Led,
    BridgeProfileSanitization Sanitization
);

public sealed record BridgeProfileMatch(
    int? Vid,
    int? Pid,
    string? NameContains
);

public sealed record BridgeProfileLed(
    BridgeLedMode Mode,
    int Channel,
    byte OnValue,
    byte OffValue,
    IReadOnlyList<string> Init
);

public enum BridgeLedMode
{
    Note,
    ControlChange,
}

public sealed record BridgeProfileSanitization(
    int MaxMessagesPerSecond,
    int CoalesceWindowMs
);

