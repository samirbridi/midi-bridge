namespace Bridge.Core.Profiles.ProfileStore;

public sealed class ProfileStore
{
    private readonly ProfileStoreCache _cache;
    private readonly ProfileStoreClient _client;
    private readonly ProfileStoreOptions _options;
    private readonly object _gate = new();
    private DateTimeOffset _lastUpdateAttempt = DateTimeOffset.MinValue;

    public ProfileStore(ProfileStoreCache cache, ProfileStoreClient client, ProfileStoreOptions? options = null)
    {
        _cache = cache;
        _client = client;
        _options = options ?? ProfileStoreOptions.Default;
        _cache.EnsureCreated();
    }

    public async Task<ProfileManifest?> UpdateAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_lastUpdateAttempt != DateTimeOffset.MinValue &&
                DateTimeOffset.UtcNow - _lastUpdateAttempt < TimeSpan.FromMinutes(1))
            {
                return _cache.TryReadManifest();
            }

            _lastUpdateAttempt = DateTimeOffset.UtcNow;
        }

        var manifestJson = await _client.DownloadManifestAsync(cancellationToken).ConfigureAwait(false);
        var manifest = ProfileStoreValidator.ParseAndValidateManifest(manifestJson, _options.MaxManifestSizeBytes);
        _cache.WriteManifest(manifest);

        var state = _cache.ReadState();

        foreach (var entry in manifest.Profiles)
        {
            if (IsQuarantined(state, entry.Id, entry.Sha256))
            {
                continue;
            }

            if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (_cache.HasActiveProfile(entry.Id))
            {
                var existing = _cache.TryReadActiveProfileJson(entry.Id);
                if (existing is not null)
                {
                    var existingHash = ProfileStoreValidator.ComputeSha256Hex(existing);
                    if (string.Equals(existingHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
            }

            var profileJson = await _client.DownloadProfileAsync(uri, _options.MaxProfileSizeBytes, cancellationToken).ConfigureAwait(false);
            var hash = ProfileStoreValidator.ComputeSha256Hex(profileJson);
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Quarantine(state, entry.Id, entry.Sha256);
                continue;
            }

            try
            {
                _ = ProfileLoader.Load(profileJson);
            }
            catch
            {
                Quarantine(state, entry.Id, entry.Sha256);
                continue;
            }

            _cache.SaveActiveProfile(entry.Id, profileJson);
        }

        _cache.WriteState(state);
        return manifest;
    }

    public ProfileManifest? GetCachedManifest() => _cache.TryReadManifest();

    public BridgeProfile? TryGetProfile(string profileId)
    {
        var json = _cache.TryReadActiveProfileJson(profileId);
        if (json is null)
        {
            return null;
        }

        return ProfileLoader.Load(json);
    }

    public BridgeProfile? ResolveProfileForDevice(int? vid, int? pid, string? name)
    {
        var manifest = _cache.TryReadManifest();
        if (manifest is null)
        {
            return null;
        }

        var entry = ProfileResolver.ResolveBest(manifest, vid, pid, name);
        if (entry is null)
        {
            return null;
        }

        return TryGetProfile(entry.Id);
    }

    public bool ReportProfileFailed(string profileId, string sha256, string reason)
    {
        var state = _cache.ReadState();
        Quarantine(state, profileId, sha256);
        _cache.WriteState(state);
        return _cache.Rollback(profileId);
    }

    private static bool IsQuarantined(ProfileStoreState state, string profileId, string sha256)
    {
        if (!state.Quarantined.TryGetValue(profileId, out var hashes))
        {
            return false;
        }

        return hashes.Contains(sha256);
    }

    private static void Quarantine(ProfileStoreState state, string profileId, string sha256)
    {
        if (!state.Quarantined.TryGetValue(profileId, out var hashes))
        {
            hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            state.Quarantined[profileId] = hashes;
        }

        hashes.Add(sha256);
    }
}
