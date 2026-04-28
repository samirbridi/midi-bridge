namespace Bridge.Core.Profiles;

public static class BuiltInProfiles
{
    public static string GenericJson { get; } =
        ProfileLoader.Serialize(new BridgeProfile(
            Id: "generic",
            DisplayName: "Genérico",
            Match: new BridgeProfileMatch(Vid: null, Pid: null, NameContains: null),
            Led: new BridgeProfileLed(BridgeLedMode.Note, Channel: 0, OnValue: 127, OffValue: 0, Init: Array.Empty<string>()),
            Sanitization: new BridgeProfileSanitization(MaxMessagesPerSecond: 4000, CoalesceWindowMs: 5)
        ));

    public static string AkaiGenericJson { get; } =
        ProfileLoader.Serialize(new BridgeProfile(
            Id: "akai-generic",
            DisplayName: "Akai (Genérico)",
            Match: new BridgeProfileMatch(Vid: 0x09E8, Pid: null, NameContains: "AKAI"),
            Led: new BridgeProfileLed(BridgeLedMode.Note, Channel: 0, OnValue: 127, OffValue: 0, Init: Array.Empty<string>()),
            Sanitization: new BridgeProfileSanitization(MaxMessagesPerSecond: 4000, CoalesceWindowMs: 5)
        ));

    public static IReadOnlyDictionary<string, string> AllJson { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["generic"] = GenericJson,
        ["akai-generic"] = AkaiGenericJson
    };
}

