namespace Bridge.Core.Profiles.ProfileStore;

public static class ProfileStorePaths
{
    public static string GetDefaultRoot(string appName = "UsbMidiBridge")
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Path.GetTempPath();
        }

        return Path.Combine(baseDir, appName, "ProfileStore");
    }
}

