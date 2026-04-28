namespace Bridge.Service;

using System.Runtime.InteropServices;
using Bridge.Core.Led;
using Bridge.Core.Midi;
using Bridge.Core.Profiles;
using Bridge.Core.Profiles.ProfileStore;
using Bridge.Core.Routing;
using Bridge.Core.Sanitization;
using Bridge.IO.Midi1.WinMM;
using Bridge.IO.VirtualMidi.TeVirtualMidi;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WinMmMidiEnumerator _enumerator = new();
    private readonly WinMmVidPidResolver _vidPidResolver = new();
    private readonly BridgeStatusState _statusState;
    private readonly ProfileStore _profileStore;
    private readonly ProfileStoreOptions _profileStoreOptions;
    private readonly Dictionary<string, string> _lastProfileByInputName = new(StringComparer.OrdinalIgnoreCase);
    private readonly LedStateCache _ledStateCache = new();
    private DeviceSession? _session;
    private string? _sessionKey;
    private string? _lastInputName;

    public Worker(ILogger<Worker> logger, BridgeStatusState statusState)
    {
        _logger = logger;
        _statusState = statusState;
        var cacheRoot = ProfileStorePaths.GetDefaultRoot();
        var cache = new ProfileStoreCache(cacheRoot);
        SeedBuiltInProfiles(cache);

        var manifestUrl = Environment.GetEnvironmentVariable("USB_MIDI_BRIDGE_PROFILE_MANIFEST_URL");
        var options = ProfileStoreOptions.Default;
        if (!string.IsNullOrWhiteSpace(manifestUrl) && Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri))
        {
            options = options with { ManifestUri = manifestUri };
        }

        var http = new HttpClient();
        var client = new ProfileStoreClient(http, options);
        _profileStore = new ProfileStore(cache, client, options);
        _profileStoreOptions = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("Bridge.Service is running on a non-Windows OS; MIDI backends are disabled.");
        }

        _ = Task.Run(() => RunProfileUpdaterAsync(stoppingToken), stoppingToken);

        var lastSnapshot = "";

        while (!stoppingToken.IsCancellationRequested)
        {
            var inputs = _enumerator.ListInputs();
            var outputs = _enumerator.ListOutputs();

            var snapshot = $"IN={inputs.Count} OUT={outputs.Count}";
            if (!string.Equals(snapshot, lastSnapshot, StringComparison.Ordinal))
            {
                _logger.LogInformation("MIDI devices: {snapshot}", snapshot);
                lastSnapshot = snapshot;
            }

            LogProfileSelection(inputs);
            await EnsureSessionAsync(inputs, outputs, stoppingToken).ConfigureAwait(false);

            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task RunProfileUpdaterAsync(CancellationToken stoppingToken)
    {
        await TryUpdateAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_profileStoreOptions.UpdateInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await TryUpdateAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task TryUpdateAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _profileStore.UpdateAsync(stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("Profile store updated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile store update failed");
        }
    }

    private static void SeedBuiltInProfiles(ProfileStoreCache cache)
    {
        foreach (var kv in BuiltInProfiles.AllJson)
        {
            if (!cache.HasActiveProfile(kv.Key))
            {
                cache.SaveActiveProfile(kv.Key, kv.Value);
            }
        }
    }

    private void LogProfileSelection(IReadOnlyList<WinMmMidiDeviceInfo> inputs)
    {
        foreach (var input in inputs)
        {
            var resolved = ResolveProfileForInput(input, out var vid, out var pid);
            var id = resolved?.Id ?? "none";

            if (_lastProfileByInputName.TryGetValue(input.Name, out var last) && string.Equals(last, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _lastProfileByInputName[input.Name] = id;
            _logger.LogInformation("Profile selected for '{device}': {profileId} (vid={vid} pid={pid})", input.Name, id, vid, pid);
        }
    }

    private static BridgeProfile? ResolveBuiltInFallback(string deviceName)
    {
        if (deviceName.Contains("AKAI", StringComparison.OrdinalIgnoreCase))
        {
            return ProfileLoader.Load(BuiltInProfiles.AkaiGenericJson);
        }

        return ProfileLoader.Load(BuiltInProfiles.GenericJson);
    }

    private async Task EnsureSessionAsync(
        IReadOnlyList<WinMmMidiDeviceInfo> inputs,
        IReadOnlyList<WinMmMidiDeviceInfo> outputs,
        CancellationToken stoppingToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var physicalInputs = inputs.Where(IsPhysicalDeviceName).ToArray();
        var physicalOutputs = outputs.Where(IsPhysicalDeviceName).ToArray();

        if (physicalInputs.Length == 0 || physicalOutputs.Length == 0)
        {
            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
                _session = null;
                _sessionKey = null;
                _logger.LogInformation("Bridge session stopped (no devices)");
            }

            _statusState.SetIdle("no devices");
            return;
        }

        var input = PickInput(physicalInputs);
        var output = PickOutput(physicalOutputs, input.Name);

        var key = $"{input.DeviceId}:{output.DeviceId}:{input.Name}:{output.Name}";
        if (string.Equals(_sessionKey, key, StringComparison.Ordinal))
        {
            return;
        }

        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
            _sessionKey = null;
        }

        if (!string.Equals(_lastInputName, input.Name, StringComparison.OrdinalIgnoreCase))
        {
            _ledStateCache.Clear();
        }
        _lastInputName = input.Name;

        var profile = ResolveProfileForInput(input, out var vid, out var pid);
        if (profile is null)
        {
            _logger.LogWarning("No profile resolved for '{device}', skipping session creation", input.Name);
            return;
        }

        try
        {
            _session = new DeviceSession(_logger, input, output, profile, _ledStateCache, stoppingToken);
            _sessionKey = key;
            _logger.LogInformation(
                "Bridge session started: IN='{inName}' OUT='{outName}' VPORTS='{keysPort}','{ledsPort}' PROFILE='{profileId}' (vid={vid} pid={pid})",
                input.Name,
                output.Name,
                _session.KeysPortName,
                _session.LedsPortName,
                profile.Id,
                vid,
                pid
            );
            _statusState.SetRunning(
                inputName: input.Name,
                outputName: output.Name,
                profileId: profile.Id,
                vid: vid,
                pid: pid,
                keysPortName: _session.KeysPortName,
                ledsPortName: _session.LedsPortName
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start bridge session");
            _session = null;
            _sessionKey = null;
            _statusState.SetError("failed to start session");
        }
    }

    private static bool IsPhysicalDeviceName(WinMmMidiDeviceInfo d)
    {
        if (d.Name.EndsWith(" - KEYS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (d.Name.EndsWith(" - LEDs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private WinMmMidiDeviceInfo PickInput(IReadOnlyList<WinMmMidiDeviceInfo> inputs)
    {
        var forced = Environment.GetEnvironmentVariable("USB_MIDI_BRIDGE_DEVICE_IN_CONTAINS");
        if (!string.IsNullOrWhiteSpace(forced))
        {
            var match = inputs.FirstOrDefault(i => i.Name.Contains(forced, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        WinMmMidiDeviceInfo? best = null;
        var bestScore = int.MinValue;

        foreach (var input in inputs)
        {
            int? vid = null;
            int? pid = null;
            if (_vidPidResolver.TryResolve(input.Name, out var v, out var p))
            {
                vid = v;
                pid = p;
            }

            var profile = _profileStore.ResolveProfileForDevice(vid, pid, input.Name) ?? ResolveBuiltInFallback(input.Name);
            var score = 0;
            if (profile is not null && !string.Equals(profile.Id, "generic", StringComparison.OrdinalIgnoreCase))
            {
                score += vid is not null && pid is not null ? 200 : 100;
            }

            if (profile is not null)
            {
                score += 10;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = input;
            }
        }

        return best ?? inputs[0];
    }

    private BridgeProfile? ResolveProfileForInput(WinMmMidiDeviceInfo input, out int? vid, out int? pid)
    {
        vid = null;
        pid = null;
        if (_vidPidResolver.TryResolve(input.Name, out var v, out var p))
        {
            vid = v;
            pid = p;
        }

        return _profileStore.ResolveProfileForDevice(vid, pid, input.Name) ?? ResolveBuiltInFallback(input.Name);
    }

    private static WinMmMidiDeviceInfo PickOutput(IReadOnlyList<WinMmMidiDeviceInfo> outputs, string inputName)
    {
        var forced = Environment.GetEnvironmentVariable("USB_MIDI_BRIDGE_DEVICE_OUT_CONTAINS");
        if (!string.IsNullOrWhiteSpace(forced))
        {
            var match = outputs.FirstOrDefault(o => o.Name.Contains(forced, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        var byName = outputs.FirstOrDefault(o =>
            o.Name.Contains(inputName, StringComparison.OrdinalIgnoreCase)
            || inputName.Contains(o.Name, StringComparison.OrdinalIgnoreCase));

        return byName ?? outputs[0];
    }

    private sealed class DeviceSession : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts;
        private readonly WinMmMidiInput _physicalIn;
        private readonly WinMmMidiOutput _physicalOut;
        private readonly TeVirtualMidiPort _keysPort;
        private readonly TeVirtualMidiPort _ledsPort;
        private readonly Task _routeInTask;
        private readonly Task _routeLedsTask;

        public string KeysPortName { get; }
        public string LedsPortName { get; }

        public DeviceSession(
            ILogger logger,
            WinMmMidiDeviceInfo input,
            WinMmMidiDeviceInfo output,
            BridgeProfile profile,
            LedStateCache ledStateCache,
            CancellationToken stoppingToken)
        {
            _logger = logger;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            var baseName = SanitizePortNameBase(input.Name);
            KeysPortName = BuildPortName(baseName, " - KEYS", 60);
            LedsPortName = BuildPortName(baseName, " - LEDs", 60);

            _physicalIn = new WinMmMidiInput(input.DeviceId);
            _physicalOut = new WinMmMidiOutput(output.DeviceId);
            _keysPort = new TeVirtualMidiPort(KeysPortName);
            _ledsPort = new TeVirtualMidiPort(LedsPortName);

            ReplayLedSnapshotAsync(_physicalOut, ledStateCache, _cts.Token).GetAwaiter().GetResult();

            var options = new MidiSanitizerOptions(
                CoalesceWindow: TimeSpan.FromMilliseconds(profile.Sanitization.CoalesceWindowMs),
                MaxMessagesPerSecondPerRoute: profile.Sanitization.MaxMessagesPerSecond
            );

            var routerIn = new MidiRouter(new MidiSanitizer(options));
            var routerLeds = new MidiRouter(new MidiSanitizer(options));

            _routeInTask = Task.Run(() => routerIn.RunAsync(_physicalIn, _keysPort, _cts.Token), _cts.Token);
            _routeLedsTask = Task.Run(
                () => routerLeds.RunAsync(_ledsPort, new LedCachingSink(ledStateCache, _physicalOut), _cts.Token),
                _cts.Token
            );
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
                await Task.WhenAll(_routeInTask, _routeLedsTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Bridge session tasks ended with error");
            }

            await _physicalIn.DisposeAsync().ConfigureAwait(false);
            await _physicalOut.DisposeAsync().ConfigureAwait(false);
            await _keysPort.DisposeAsync().ConfigureAwait(false);
            await _ledsPort.DisposeAsync().ConfigureAwait(false);
            _cts.Dispose();
        }

        private static async Task ReplayLedSnapshotAsync(WinMmMidiOutput output, LedStateCache cache, CancellationToken cancellationToken)
        {
            var snapshot = cache.Snapshot();
            foreach (var bytes in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await output.WriteAsync(Midi1Message.FromArray(DateTimeOffset.UtcNow, bytes), cancellationToken).ConfigureAwait(false);
            }
        }

        private static string SanitizePortNameBase(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "USB MIDI Bridge";
            }

            var cleaned = name.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return "USB MIDI Bridge";
            }

            return cleaned;
        }

        private static string BuildPortName(string baseName, string suffix, int maxTotalLength)
        {
            var available = Math.Max(1, maxTotalLength - suffix.Length);
            if (baseName.Length > available)
            {
                baseName = baseName[..available];
            }

            return baseName + suffix;
        }

        private sealed class LedCachingSink : IMidiMessageSink
        {
            private readonly LedStateCache _cache;
            private readonly IMidiMessageSink _inner;

            public LedCachingSink(LedStateCache cache, IMidiMessageSink inner)
            {
                _cache = cache;
                _inner = inner;
            }

            public ValueTask WriteAsync(Midi1Message message, CancellationToken cancellationToken = default)
            {
                _cache.TryApply(message, out _);
                return _inner.WriteAsync(message, cancellationToken);
            }
        }
    }
}
