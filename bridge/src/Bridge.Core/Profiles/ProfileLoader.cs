using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bridge.Core.Profiles;

public static class ProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    static ProfileLoader()
    {
        Options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public static BridgeProfile Load(string json)
    {
        var profile = JsonSerializer.Deserialize<BridgeProfile>(json, Options);
        if (profile is null)
        {
            throw new InvalidOperationException("Invalid profile JSON");
        }

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            throw new InvalidOperationException("Profile id is required");
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            throw new InvalidOperationException("Profile displayName is required");
        }

        return profile;
    }

    public static string Serialize(BridgeProfile profile) => JsonSerializer.Serialize(profile, Options);
}
