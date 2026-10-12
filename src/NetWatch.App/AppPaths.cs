namespace NetWatch.App;

internal static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\NetWatch; wird bei Bedarf angelegt.</summary>
    public static string DataDirectory
    {
        get
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetWatch");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
