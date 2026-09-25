using System.IO;
using AzureCreditsApp.Models;

namespace AzureCreditsApp.Services;

public interface IMonthlyCreditStore
{
    MonthlyCredit? Get(string subscriptionId);

    /// <summary>
    /// Saves the monthly credit for a subscription, or clears it when <paramref name="credit"/> is null.
    /// </summary>
    void Set(string subscriptionId, MonthlyCredit? credit);
}

public sealed class MonthlyCreditStore : IMonthlyCreditStore
{
    private readonly string _filePath;
    private readonly Dictionary<string, MonthlyCredit> _credits;

    public MonthlyCreditStore()
        : this(Path.Combine(AppPaths.DataDirectory, "monthly-credits.json"))
    {
    }

    public MonthlyCreditStore(string filePath)
    {
        _filePath = filePath;
        _credits = new Dictionary<string, MonthlyCredit>(
            JsonFile.Read<Dictionary<string, MonthlyCredit>>(filePath) ?? new Dictionary<string, MonthlyCredit>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public MonthlyCredit? Get(string subscriptionId)
    {
        return _credits.GetValueOrDefault(subscriptionId);
    }

    public void Set(string subscriptionId, MonthlyCredit? credit)
    {
        if (credit is null)
        {
            _credits.Remove(subscriptionId);
        }
        else
        {
            _credits[subscriptionId] = credit;
        }

        JsonFile.Write(_filePath, _credits);
    }
}
