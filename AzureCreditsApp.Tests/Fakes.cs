using Azure.Core;
using Azure.Identity;
using AzureCreditsApp.Models;
using AzureCreditsApp.Services;

namespace AzureCreditsApp.Tests;

internal sealed class FakeAccountService : IAccountService
{
    public List<AccountInfo> Saved { get; } = [];

    public AccountInfo? NextAccountToAdd { get; set; }

    public Exception? AddAccountError { get; set; }

    public Task<IReadOnlyList<AccountInfo>> LoadAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AccountInfo>>(Saved.ToList());
    }

    public Task<AccountInfo> AddAccountAsync(CancellationToken cancellationToken = default)
    {
        if (AddAccountError is not null)
        {
            return Task.FromException<AccountInfo>(AddAccountError);
        }

        AccountInfo account = NextAccountToAdd ?? throw new InvalidOperationException("No account to add.");
        Saved.Add(account);
        return Task.FromResult(account);
    }

    public Task ReauthenticateAsync(AccountInfo account, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RemoveAccountAsync(AccountInfo account, CancellationToken cancellationToken = default)
    {
        Saved.Remove(account);
        return Task.CompletedTask;
    }

    public TokenCredential GetCredential(AccountInfo account, string? tenantId = null)
    {
        throw new NotSupportedException("Tests use FakeAzureCreditService.");
    }
}

internal sealed class FakeAzureCreditService : IAzureCreditService
{
    public List<SubscriptionData> Subscriptions { get; } = [];

    public List<string> Warnings { get; } = [];

    public HashSet<string> AccountsNeedingSignIn { get; } = [];

    public int LoadCount { get; private set; }

    public Task<CreditSnapshot> LoadAsync(
        IReadOnlyList<AccountInfo> accounts,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LoadCount++;
        HashSet<string> accountIds = accounts.Select(account => account.Id).ToHashSet();
        CreditSnapshot snapshot = new(
            Subscriptions.Where(data => accountIds.Contains(data.Subscription.Account.Id)).ToList(),
            Warnings.ToList(),
            AccountsNeedingSignIn.ToHashSet(),
            TestData.Now);
        return Task.FromResult(snapshot);
    }
}

internal sealed class InMemoryMonthlyCreditStore : IMonthlyCreditStore
{
    private readonly Dictionary<string, MonthlyCredit> _credits = new(StringComparer.OrdinalIgnoreCase);

    public MonthlyCredit? Get(string subscriptionId) => _credits.GetValueOrDefault(subscriptionId);

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
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class TestData
{
    /// <summary>Noon UTC keeps the local date stable in any time zone the tests run in.</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = new(2026, 9, 25);

    public static readonly AccountInfo Account = new("account-1", "someone@outlook.com");

    public static readonly AccountInfo OtherAccount = new("account-2", "someone@contoso.com");

    public static SubscriptionInfo Subscription(
        string name,
        string quotaId,
        string spendingLimit = "Off",
        string state = "Enabled",
        AccountInfo? account = null,
        string? id = null)
    {
        return new SubscriptionInfo(
            id ?? Guid.NewGuid().ToString(),
            name,
            state,
            "tenant-1",
            "Default Directory",
            account ?? Account,
            quotaId,
            spendingLimit,
            OfferClassifier.Classify(quotaId));
    }

    public static BillingProfileCredit Profile(
        decimal original,
        decimal remaining,
        DateTimeOffset expires,
        string id = "profile-1",
        string currency = "USD")
    {
        CreditLot lot = new("Azure sponsorship credit", original, remaining, currency, expires.AddYears(-1), expires);
        return new BillingProfileCredit(id, "Personal", currency, remaining, remaining, [lot]);
    }

    public static CostHistory Costs(string currency, params (DateOnly Date, decimal Amount)[] days)
    {
        return new CostHistory(currency, days.Select(day => new DailyCost(day.Date, day.Amount)).ToList(), Now);
    }

    public static SubscriptionData Sponsorship(decimal original = 12000, decimal remaining = 12000, int daysToExpiry = 325, AccountInfo? account = null)
    {
        return new SubscriptionData(
            Subscription("Azure subscription 2026", "Sponsored_2016-01-01", account: account),
            Profile(original, remaining, Now.AddDays(daysToExpiry)),
            SubscriptionsSharingProfile: 1,
            Costs: Costs("USD"));
    }

    public static SubscriptionData VisualStudio(decimal periodCost = 419.34m, string currency = "ARS")
    {
        return new SubscriptionData(
            Subscription("Visual Studio Enterprise Subscription", "MSDN_2014-09-01", spendingLimit: "On"),
            Costs: Costs(currency, (new DateOnly(2026, 9, 2), periodCost), (new DateOnly(2026, 8, 20), 559.28m)),
            BillingPeriod: new BillingPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
    }

    public static SubscriptionData PayAsYouGo(int resources = 29, decimal monthCost = 1.39m)
    {
        return new SubscriptionData(
            Subscription("Microsoft Azure Sponsorship", "PayAsYouGo_2014-09-01"),
            Costs: Costs("ARS", (new DateOnly(2026, 9, 10), monthCost)),
            ResourceCount: resources);
    }
}

internal static class Errors
{
    public static AuthenticationFailedException SignInCancelled() => new("The user cancelled the sign-in.");
}
