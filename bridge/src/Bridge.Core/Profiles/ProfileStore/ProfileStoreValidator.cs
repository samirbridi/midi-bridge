using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bridge.Core.Profiles.ProfileStore;

public static class ProfileStoreValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ProfileManifest ParseAndValidateManifest(string json, long maxSizeBytes)
    {
        if (Encoding.UTF8.GetByteCount(json) > maxSizeBytes)
        {
            throw new InvalidOperationException("Manifest is too large");
        }

        var manifest = JsonSerializer.Deserialize<ProfileManifest>(json, JsonOptions);
        if (manifest is null)
        {
            throw new InvalidOperationException("Invalid manifest JSON");
        }

        if (manifest.ManifestVersion <= 0)
        {
            throw new InvalidOperationException("ManifestVersion must be > 0");
        }

        if (manifest.Profiles is null)
        {
            throw new InvalidOperationException("Manifest profiles missing");
        }

        foreach (var p in manifest.Profiles)
        {
            ValidateManifestEntry(p);
        }

        return manifest;
    }

    public static void ValidateManifestEntry(ProfileManifestEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            throw new InvalidOperationException("Profile id is required");
        }

        if (string.IsNullOrWhiteSpace(entry.DisplayName))
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' displayName is required");
        }

        if (entry.SchemaVersion <= 0)
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' schemaVersion must be > 0");
        }

        if (entry.Match is null)
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' match is required");
        }

        if (entry.Match.Vid is null && entry.Match.Pid is null && string.IsNullOrWhiteSpace(entry.Match.NameContains))
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' match cannot be empty");
        }

        if (entry.Match.Vid is not null && (entry.Match.Vid < 0 || entry.Match.Vid > 0xFFFF))
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' invalid vid");
        }

        if (entry.Match.Pid is not null && (entry.Match.Pid < 0 || entry.Match.Pid > 0xFFFF))
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' invalid pid");
        }

        if (string.IsNullOrWhiteSpace(entry.Url) || !Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' url must be https");
        }

        if (string.IsNullOrWhiteSpace(entry.Sha256) || entry.Sha256.Length != 64 || entry.Sha256.Any(c => !"0123456789abcdefABCDEF".Contains(c)))
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' sha256 must be hex-encoded");
        }

        if (entry.SizeBytes <= 0)
        {
            throw new InvalidOperationException($"Profile '{entry.Id}' sizeBytes must be > 0");
        }
    }

    public static string ComputeSha256Hex(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

