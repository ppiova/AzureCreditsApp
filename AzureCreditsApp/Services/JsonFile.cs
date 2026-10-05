using System.IO;
using System.Text.Json;

namespace AzureCreditsApp.Services;

internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (JsonException)
        {
            // A corrupt file should not stop the app; it is rewritten on the next save.
            return default;
        }
    }

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tempPath = $"{path}.tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, value, Options);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
