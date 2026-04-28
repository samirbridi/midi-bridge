using Bridge.Core.Status;

namespace Bridge.Service;

public sealed class BridgeStatusState
{
    private readonly object _gate = new();
    private BridgeStatus _status = new(
        ServiceState: "starting",
        SelectedInputName: null,
        SelectedOutputName: null,
        ProfileId: null,
        Vid: null,
        Pid: null,
        KeysPortName: null,
        LedsPortName: null,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Message: null
    );

    public BridgeStatus Snapshot()
    {
        lock (_gate)
        {
            return _status;
        }
    }

    public void SetIdle(string? message = null)
    {
        lock (_gate)
        {
            _status = _status with
            {
                ServiceState = "idle",
                SelectedInputName = null,
                SelectedOutputName = null,
                ProfileId = null,
                Vid = null,
                Pid = null,
                KeysPortName = null,
                LedsPortName = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Message = message
            };
        }
    }

    public void SetRunning(
        string inputName,
        string outputName,
        string profileId,
        int? vid,
        int? pid,
        string keysPortName,
        string ledsPortName,
        string? message = null)
    {
        lock (_gate)
        {
            _status = new BridgeStatus(
                ServiceState: "running",
                SelectedInputName: inputName,
                SelectedOutputName: outputName,
                ProfileId: profileId,
                Vid: vid,
                Pid: pid,
                KeysPortName: keysPortName,
                LedsPortName: ledsPortName,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Message: message
            );
        }
    }

    public void SetError(string message)
    {
        lock (_gate)
        {
            _status = _status with
            {
                ServiceState = "error",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Message = message
            };
        }
    }
}

