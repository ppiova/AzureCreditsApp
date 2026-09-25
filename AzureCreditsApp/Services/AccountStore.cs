using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;

namespace AzureCreditsApp.Services;

/// <summary>
/// Saves one <see cref="AuthenticationRecord"/> per signed-in account. The record
/// only identifies the account; tokens stay in the OS-protected MSAL cache.
/// </summary>
public sealed class AccountStore
{
    private readonly string _directory;

    public AccountStore()
        : this(Path.Combine(AppPaths.DataDirectory, "accounts"))
    {
    }

    public AccountStore(string directory)
    {
        _directory = directory;
    }

    public async Task<IReadOnlyList<AuthenticationRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        List<AuthenticationRecord> records = [];
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                await using FileStream stream = File.OpenRead(file);
                records.Add(await AuthenticationRecord.DeserializeAsync(stream, cancellationToken));
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
            {
                // Skip a damaged record; the user can add the account again.
            }
        }

        return records
            .OrderBy(record => record.Username, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task SaveAsync(AuthenticationRecord record, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);

        string path = PathFor(record.HomeAccountId);
        string tempPath = $"{path}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await record.SerializeAsync(stream, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public void Delete(string homeAccountId)
    {
        string path = PathFor(homeAccountId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(string homeAccountId)
    {
        // Home account ids contain characters that are not valid in file names.
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(homeAccountId)));
        return Path.Combine(_directory, $"{hash[..16].ToLowerInvariant()}.json");
    }
}
