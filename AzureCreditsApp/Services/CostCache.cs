using System.IO;
using AzureCreditsApp.Models;

namespace AzureCreditsApp.Services;

/// <summary>
/// Keeps the last cost history per subscription. Cost Management throttles
/// aggressively and only refreshes its data a few times a day, so the app
/// reuses recent results and falls back to older ones when a query fails.
/// </summary>
public sealed class CostCache
{
    private readonly string _directory;

    public CostCache()
        : this(Path.Combine(AppPaths.DataDirectory, "cache", "costs"))
    {
    }

    public CostCache(string directory)
    {
        _directory = directory;
    }

    public CostHistory? Get(string subscriptionId)
    {
        return JsonFile.Read<CostHistory>(PathFor(subscriptionId));
    }

    public void Save(string subscriptionId, CostHistory history)
    {
        JsonFile.Write(PathFor(subscriptionId), history);
    }

    private string PathFor(string subscriptionId)
    {
        return Path.Combine(_directory, $"{subscriptionId.ToLowerInvariant()}.json");
    }
}
