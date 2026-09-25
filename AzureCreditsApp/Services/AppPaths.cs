using System.IO;

namespace AzureCreditsApp.Services;

public static class AppPaths
{
    /// <summary>
    /// Per-user data folder. Inside the MSIX package Windows redirects it to the
    /// package's private AppData, so it is removed when the app is uninstalled.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AzureCreditsApp");
}
