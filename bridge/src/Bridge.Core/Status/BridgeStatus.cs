namespace Bridge.Core.Status;

public sealed record BridgeStatus(
    string ServiceState,
    string? SelectedInputName,
    string? SelectedOutputName,
    string? ProfileId,
    int? Vid,
    int? Pid,
    string? KeysPortName,
    string? LedsPortName,
    DateTimeOffset UpdatedAtUtc,
    string? Message
);

