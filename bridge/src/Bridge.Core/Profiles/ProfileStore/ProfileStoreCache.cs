using System.Text.Json;

namespace Bridge.Core.Profiles.ProfileStore;

public sealed class ProfileStoreCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ProfileStoreCache(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        ManifestPath = Path.Combine(RootDirectory, "manifest.json");
        StatePath = Path.Combine(RootDirectory, "state.json");
        ProfilesDirectory = Path.Combine(RootDirectory, "profiles");
    }

    public string RootDirectory { get; }
    public string ManifestPath { get; }
    public string StatePath { get; }
    public string ProfilesDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
    }

    public string GetProfileDirectory(string profileId) => Path.Combine(ProfilesDirectory, profileId);

    public string GetActiveProfilePath(string profileId) => Path.Combine(GetProfileDirectory(profileId), "active.json");

    public string GetPreviousProfilePath(string profileId) => Path.Combine(GetProfileDirectory(profileId), "previous.json");

    public void WriteManifest(ProfileManifest manifest)
    {
        EnsureCreated();
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(ManifestPath, json);
    }

    public ProfileManifest? TryReadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            return null;
        }

        var json = File.ReadAllText(ManifestPath);
        return JsonSerializer.Deserialize<ProfileManifest>(json, JsonOptions);
    }

    public ProfileStoreState ReadState()
    {
        if (!File.Exists(StatePath))
        {
            return new ProfileStoreState(Version: 1, Quarantined: new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
        }

        var json = File.ReadAllText(StatePath);
        return JsonSerializer.Deserialize<ProfileStoreState>(json, JsonOptions)
            ?? new ProfileStoreState(Version: 1, Quarantined: new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
    }

    public void WriteState(ProfileStoreState state)
    {
        EnsureCreated();
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(StatePath, json);
    }

    public bool HasActiveProfile(string profileId) => File.Exists(GetActiveProfilePath(profileId));

    public string? TryReadActiveProfileJson(string profileId)
    {
        var path = GetActiveProfilePath(profileId);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void SaveActiveProfile(string profileId, string profileJson)
    {
        EnsureCreated();
        var dir = GetProfileDirectory(profileId);
        Directory.CreateDirectory(dir);

        var activePath = GetActiveProfilePath(profileId);
        var previousPath = GetPreviousProfilePath(profileId);

        if (File.Exists(activePath))
        {
            File.Copy(activePath, previousPath, overwrite: true);
        }

        File.WriteAllText(activePath, profileJson);
    }

    public bool Rollback(string profileId)
    {
        var activePath = GetActiveProfilePath(profileId);
        var previousPath = GetPreviousProfilePath(profileId);
        if (!File.Exists(previousPath))
        {
            return false;
        }

        File.Copy(previousPath, activePath, overwrite: true);
        return true;
    }
}

public sealed record ProfileStoreState(
    int Version,
    Dictionary<string, HashSet<string>> Quarantined
);

