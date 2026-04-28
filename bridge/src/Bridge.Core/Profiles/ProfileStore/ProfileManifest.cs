using System.Text.Json.Serialization;

namespace Bridge.Core.Profiles.ProfileStore;

public sealed record ProfileManifest(
    int ManifestVersion,
    IReadOnlyList<ProfileManifestEntry> Profiles
);

public sealed record ProfileManifestEntry(
    string Id,
    string DisplayName,
    int SchemaVersion,
    ProfileManifestMatch Match,
    string Url,
    string Sha256,
    long SizeBytes,
    IReadOnlyList<string>? Tags
)
{
    [JsonIgnore]
    public bool HasVidPid => Match.Vid is not null && Match.Pid is not null;
}

public sealed record ProfileManifestMatch(
    int? Vid,
    int? Pid,
    string? NameContains
);

