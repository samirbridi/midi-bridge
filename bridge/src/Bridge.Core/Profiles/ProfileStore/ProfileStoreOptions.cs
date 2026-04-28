namespace Bridge.Core.Profiles.ProfileStore;

public sealed record ProfileStoreOptions(
    Uri ManifestUri,
    TimeSpan UpdateInterval,
    long MaxProfileSizeBytes,
    long MaxManifestSizeBytes
)
{
    public static ProfileStoreOptions Default { get; } = new(
        ManifestUri: new Uri("https://raw.githubusercontent.com/samirbridi/midi-bridge/refs/heads/main/profile-store/index/manifest.json"),
        UpdateInterval: TimeSpan.FromHours(12),
        MaxProfileSizeBytes: 256 * 1024,
        MaxManifestSizeBytes: 2 * 1024 * 1024
    );
}
