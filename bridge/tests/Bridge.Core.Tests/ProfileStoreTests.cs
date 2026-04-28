using Bridge.Core.Profiles.ProfileStore;

namespace Bridge.Core.Tests;

public class ProfileStoreTests
{
    [Fact]
    public void ProfileResolver_Prefers_VidPid()
    {
        var manifest = new ProfileManifest(
            ManifestVersion: 1,
            Profiles: new[]
            {
                new ProfileManifestEntry(
                    Id: "by-name",
                    DisplayName: "By Name",
                    SchemaVersion: 1,
                    Match: new ProfileManifestMatch(Vid: null, Pid: null, NameContains: "AKAI"),
                    Url: "https://example.com/p.json",
                    Sha256: new string('a', 64),
                    SizeBytes: 10,
                    Tags: null
                ),
                new ProfileManifestEntry(
                    Id: "by-vidpid",
                    DisplayName: "By VidPid",
                    SchemaVersion: 1,
                    Match: new ProfileManifestMatch(Vid: 0x09E8, Pid: 0x0001, NameContains: null),
                    Url: "https://example.com/p2.json",
                    Sha256: new string('b', 64),
                    SizeBytes: 10,
                    Tags: null
                )
            }
        );

        var selected = ProfileResolver.ResolveBest(manifest, vid: 0x09E8, pid: 0x0001, name: "AKAI APC");
        Assert.NotNull(selected);
        Assert.Equal("by-vidpid", selected!.Id);
    }

    [Fact]
    public void ProfileStoreCache_Rollback_Restores_Previous()
    {
        var dir = Path.Combine(Path.GetTempPath(), "UsbMidiBridgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var cache = new ProfileStoreCache(dir);
        cache.SaveActiveProfile("p", "{\"id\":\"p\",\"displayName\":\"v1\",\"match\":{},\"led\":{\"mode\":0,\"channel\":0,\"onValue\":127,\"offValue\":0,\"init\":[]},\"sanitization\":{\"maxMessagesPerSecond\":1,\"coalesceWindowMs\":1}}");
        cache.SaveActiveProfile("p", "{\"id\":\"p\",\"displayName\":\"v2\",\"match\":{},\"led\":{\"mode\":0,\"channel\":0,\"onValue\":127,\"offValue\":0,\"init\":[]},\"sanitization\":{\"maxMessagesPerSecond\":1,\"coalesceWindowMs\":1}}");

        Assert.True(cache.Rollback("p"));
        var active = cache.TryReadActiveProfileJson("p");
        Assert.NotNull(active);
        Assert.Contains("\"displayName\":\"v1\"", active!, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileResolver_Uses_Wildcard_As_Fallback()
    {
        var manifest = new ProfileManifest(
            ManifestVersion: 1,
            Profiles: new[]
            {
                new ProfileManifestEntry(
                    Id: "generic",
                    DisplayName: "Generic",
                    SchemaVersion: 1,
                    Match: new ProfileManifestMatch(Vid: null, Pid: null, NameContains: "*"),
                    Url: "https://example.com/p.json",
                    Sha256: new string('a', 64),
                    SizeBytes: 10,
                    Tags: null
                ),
                new ProfileManifestEntry(
                    Id: "by-name",
                    DisplayName: "By Name",
                    SchemaVersion: 1,
                    Match: new ProfileManifestMatch(Vid: null, Pid: null, NameContains: "NOVATION"),
                    Url: "https://example.com/p2.json",
                    Sha256: new string('b', 64),
                    SizeBytes: 10,
                    Tags: null
                )
            }
        );

        Assert.Equal("by-name", ProfileResolver.ResolveBest(manifest, null, null, "Novation Launchpad")!.Id);
        Assert.Equal("generic", ProfileResolver.ResolveBest(manifest, null, null, "Unknown Device")!.Id);
    }
}
