using System.IO;

namespace AzureCreditsApp.Services;

public sealed record AppSettings(bool HideNotTracked = false);

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

public sealed class AppSettingsStore : IAppSettingsStore
{
    private readonly string _filePath;

    public AppSettingsStore()
        : this(Path.Combine(AppPaths.DataDirectory, "settings.json"))
    {
    }

    public AppSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        return JsonFile.Read<AppSettings>(_filePath) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        JsonFile.Write(_filePath, settings);
    }
}
