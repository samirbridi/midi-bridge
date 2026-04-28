using Bridge.Core.Led;
using Bridge.Core.Midi;
using Bridge.Core.Profiles;
using Bridge.Core.Sanitization;

namespace Bridge.Core.Tests;

public class UnitTest1
{
    [Fact]
    public void Sanitizer_Drops_RepeatedCc_InWindow()
    {
        var sanitizer = new MidiSanitizer(new MidiSanitizerOptions(TimeSpan.FromMilliseconds(10), 1000));

        var t0 = DateTimeOffset.UtcNow;
        var msg1 = Midi1Message.FromArray(t0, new byte[] { 0xB0, 0x10, 0x7F });
        var msg2 = Midi1Message.FromArray(t0.AddMilliseconds(5), new byte[] { 0xB0, 0x10, 0x7F });

        Assert.False(sanitizer.ShouldDrop(msg1));
        Assert.True(sanitizer.ShouldDrop(msg2));
    }

    [Fact]
    public void LedStateCache_Snapshots_LastState()
    {
        var cache = new LedStateCache();
        var msg = Midi1Message.Now(new byte[] { 0x90, 0x20, 0x01 });

        Assert.True(cache.TryApply(msg, out _));
        var snap = cache.Snapshot();

        Assert.Single(snap);
        Assert.Equal(new byte[] { 0x90, 0x20, 0x01 }, snap[0]);
    }

    [Fact]
    public void ProfileLoader_RoundTrip()
    {
        var profile = new BridgeProfile(
            Id: "akai-generic",
            DisplayName: "Akai Generic",
            Match: new BridgeProfileMatch(Vid: 0x09E8, Pid: null, NameContains: "AKAI"),
            Led: new BridgeProfileLed(BridgeLedMode.Note, Channel: 0, OnValue: 127, OffValue: 0, Init: Array.Empty<string>()),
            Sanitization: new BridgeProfileSanitization(MaxMessagesPerSecond: 4000, CoalesceWindowMs: 5)
        );

        var json = ProfileLoader.Serialize(profile);
        var loaded = ProfileLoader.Load(json);

        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.DisplayName, loaded.DisplayName);
        Assert.Equal(profile.Match.Vid, loaded.Match.Vid);
    }
}
