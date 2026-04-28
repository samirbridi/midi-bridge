namespace Bridge.Core.Profiles.ProfileStore;

public static class ProfileResolver
{
    public static ProfileManifestEntry? ResolveBest(ProfileManifest manifest, int? vid, int? pid, string? name)
    {
        if (manifest.Profiles.Count == 0)
        {
            return null;
        }

        if (vid is not null && pid is not null)
        {
            var exact = manifest.Profiles.FirstOrDefault(p => p.Match.Vid == vid && p.Match.Pid == pid);
            if (exact is not null)
            {
                return exact;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var byName = manifest.Profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.Match.NameContains) && p.Match.NameContains != "*")
                .FirstOrDefault(p => name.Contains(p.Match.NameContains!, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }

            var wildcard = manifest.Profiles.FirstOrDefault(p => string.Equals(p.Match.NameContains, "*", StringComparison.Ordinal));
            if (wildcard is not null)
            {
                return wildcard;
            }
        }

        return null;
    }
}
