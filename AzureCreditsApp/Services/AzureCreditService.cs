using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using AzureCreditsApp.Models;

namespace AzureCreditsApp.Services;

public interface IAzureCreditService
{
    /// <summary>
    /// Loads every subscription the accounts can see, with the credit and cost data
    /// needed to compute its status. Partial failures become warnings in the snapshot.
    /// </summary>
    Task<CreditSnapshot> LoadAsync(
        IReadOnlyList<AccountInfo> accounts,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class AzureCreditService : IAzureCreditService
{
    private const string ResourceManagerApiVersion = "2022-12-01";
    private const string BillingApiVersion = "2024-04-01";
    private const string ConsumptionApiVersion = "2023-03-01";
    private const string CostManagementApiVersion = "2023-11-01";
    private const string BillingPeriodsApiVersion = "2018-03-01-preview";
    private const string ResourcesApiVersion = "2021-04-01";
    private const int CostHistoryMonths = 6;
    private static readonly TimeSpan CostCacheLifetime = TimeSpan.FromHours(3);

    private readonly IAccountService _accounts;
    private readonly CostCache _costCache;
    private readonly TimeProvider _time;
    private readonly Dictionary<TokenCredential, ArmRestClient> _clients = [];

    public AzureCreditService(IAccountService accounts, CostCache costCache, TimeProvider time)
    {
        _accounts = accounts;
        _costCache = costCache;
        _time = time;
    }

    public async Task<CreditSnapshot> LoadAsync(
        IReadOnlyList<AccountInfo> accounts,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<string> warnings = [];
        HashSet<string> accountsNeedingSignIn = new(StringComparer.Ordinal);
        Dictionary<string, (SubscriptionInfo Info, ArmRestClient Client)> subscriptions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BillingProfileCredit> creditBySubscription = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenBillingAccounts = new(StringComparer.OrdinalIgnoreCase);

        foreach (AccountInfo account in accounts)
        {
            progress?.Report($"Reading directories for {account.Username}...");

            IReadOnlyList<TenantEntry> tenants;
            try
            {
                tenants = await GetTenantsAsync(ClientFor(account, tenantId: null), cancellationToken);
            }
            catch (Exception ex) when (IsSignInRequired(ex))
            {
                accountsNeedingSignIn.Add(account.Id);
                AddWarning(warnings, $"{account.Username}: sign in again to load its subscriptions.");
                continue;
            }

            foreach (TenantEntry tenant in tenants)
            {
                ArmRestClient client = ClientFor(account, tenant.Id);
                List<SubscriptionInfo> found;
                try
                {
                    found = await GetSubscriptionsAsync(client, account, tenant, cancellationToken);
                }
                catch (Exception ex) when (IsSignInRequired(ex))
                {
                    AddWarning(warnings, $"{account.Username}: the directory '{tenant.Name}' needs an extra sign-in (MFA or conditional access) and was skipped.");
                    continue;
                }
                catch (RequestFailedException ex)
                {
                    AddWarning(warnings, $"{account.Username}: could not list subscriptions in '{tenant.Name}' ({Describe(ex)}).");
                    continue;
                }

                if (found.Count == 0)
                {
                    continue;
                }

                foreach (SubscriptionInfo subscription in found)
                {
                    subscriptions.TryAdd(subscription.Id, (subscription, client));
                }

                progress?.Report($"Reading credits in {tenant.Name}...");
                await LoadBillingCreditsAsync(client, seenBillingAccounts, creditBySubscription, warnings, cancellationToken);
            }
        }

        List<SubscriptionData> data = [];
        int index = 0;
        foreach ((SubscriptionInfo info, ArmRestClient client) in subscriptions.Values
            .OrderBy(entry => entry.Info.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            index++;
            progress?.Report($"Reading costs ({index}/{subscriptions.Count}): {info.Name}...");

            creditBySubscription.TryGetValue(info.Id, out BillingProfileCredit? profile);
            int sharing = profile is null
                ? 0
                : subscriptions.Keys.Count(id => creditBySubscription.TryGetValue(id, out BillingProfileCredit? other) && other.Id == profile.Id);

            CostHistory? costs = null;
            string? costError = null;
            if (profile is not null || OfferClassifier.TracksCost(info.Offer))
            {
                (costs, costError) = await GetCostHistoryAsync(client, info, cancellationToken);
            }

            BillingPeriod? period = profile is null && OfferClassifier.HasMonthlyCredit(info.Offer)
                ? await TryGetBillingPeriodAsync(client, info.Id, cancellationToken)
                : null;

            int? resourceCount = profile is null && OfferClassifier.IsBilledToPaymentMethod(info.Offer)
                ? await TryCountResourcesAsync(client, info.Id, cancellationToken)
                : null;

            data.Add(new SubscriptionData(info, profile, sharing, costs, costError, period, resourceCount));
        }

        return new CreditSnapshot(data, warnings, accountsNeedingSignIn, _time.GetUtcNow());
    }

    private ArmRestClient ClientFor(AccountInfo account, string? tenantId)
    {
        TokenCredential credential = _accounts.GetCredential(account, tenantId);
        lock (_clients)
        {
            if (!_clients.TryGetValue(credential, out ArmRestClient? client))
            {
                client = new ArmRestClient(credential);
                _clients[credential] = client;
            }

            return client;
        }
    }

    private static async Task<IReadOnlyList<TenantEntry>> GetTenantsAsync(ArmRestClient client, CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> items = await client.GetAllAsync($"/tenants?api-version={ResourceManagerApiVersion}", cancellationToken);

        return items
            .Select(item => new TenantEntry(
                JsonValues.GetString(item, "tenantId") ?? string.Empty,
                JsonValues.GetString(item, "displayName")
                    ?? JsonValues.GetString(item, "defaultDomain")
                    ?? JsonValues.GetString(item, "tenantId")
                    ?? "Directory"))
            .Where(tenant => tenant.Id.Length > 0)
            .ToList();
    }

    private static async Task<List<SubscriptionInfo>> GetSubscriptionsAsync(
        ArmRestClient client,
        AccountInfo account,
        TenantEntry tenant,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> items = await client.GetAllAsync($"/subscriptions?api-version={ResourceManagerApiVersion}", cancellationToken);
        List<SubscriptionInfo> subscriptions = [];

        foreach (JsonElement item in items)
        {
            string? id = JsonValues.GetString(item, "subscriptionId");
            string tenantId = JsonValues.GetString(item, "tenantId") ?? tenant.Id;
            if (id is null || !string.Equals(tenantId, tenant.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string quotaId = JsonValues.GetString(item, "subscriptionPolicies", "quotaId") ?? string.Empty;
            subscriptions.Add(new SubscriptionInfo(
                id,
                JsonValues.GetString(item, "displayName") ?? id,
                JsonValues.GetString(item, "state") ?? "Unknown",
                tenant.Id,
                tenant.Name,
                account,
                quotaId,
                JsonValues.GetString(item, "subscriptionPolicies", "spendingLimit") ?? "Unknown",
                OfferClassifier.Classify(quotaId)));
        }

        return subscriptions;
    }

    /// <summary>
    /// Finds Microsoft Customer Agreement billing profiles with credit lots and maps
    /// each subscription billed to them. Directories without billing access are skipped.
    /// </summary>
    private static async Task LoadBillingCreditsAsync(
        ArmRestClient client,
        HashSet<string> seenBillingAccounts,
        Dictionary<string, BillingProfileCredit> creditBySubscription,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> billingAccounts;
        try
        {
            billingAccounts = await client.GetAllAsync($"/providers/Microsoft.Billing/billingAccounts?api-version={BillingApiVersion}", cancellationToken);
        }
        catch (RequestFailedException)
        {
            return;
        }

        foreach (JsonElement billingAccount in billingAccounts)
        {
            string? accountId = JsonValues.GetString(billingAccount, "id");
            string? agreement = JsonValues.GetString(billingAccount, "properties", "agreementType");
            if (accountId is null
                || !string.Equals(agreement, "MicrosoftCustomerAgreement", StringComparison.OrdinalIgnoreCase)
                || !seenBillingAccounts.Add(accountId))
            {
                continue;
            }

            string accountName = JsonValues.GetString(billingAccount, "properties", "displayName") ?? accountId;
            try
            {
                Dictionary<string, BillingProfileCredit> profiles = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement profile in await client.GetAllAsync($"{accountId}/billingProfiles?api-version={BillingApiVersion}", cancellationToken))
                {
                    string? profileId = JsonValues.GetString(profile, "id");
                    if (profileId is null)
                    {
                        continue;
                    }

                    string profileName = JsonValues.GetString(profile, "properties", "displayName")
                        ?? JsonValues.GetString(profile, "name")
                        ?? profileId;
                    if (await GetProfileCreditAsync(client, profileId, profileName, cancellationToken) is BillingProfileCredit credit)
                    {
                        profiles[profileId] = credit;
                    }
                }

                if (profiles.Count == 0)
                {
                    continue;
                }

                foreach (JsonElement billingSubscription in await client.GetAllAsync($"{accountId}/billingSubscriptions?api-version={BillingApiVersion}", cancellationToken))
                {
                    string? subscriptionId = JsonValues.GetString(billingSubscription, "properties", "subscriptionId");
                    string? profileId = JsonValues.GetString(billingSubscription, "properties", "billingProfileId");
                    if (subscriptionId is not null
                        && profileId is not null
                        && profiles.TryGetValue(profileId, out BillingProfileCredit? credit))
                    {
                        creditBySubscription[subscriptionId] = credit;
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                AddWarning(warnings, $"Could not read credits for billing account '{accountName}' ({Describe(ex)}).");
            }
        }
    }

    private static async Task<BillingProfileCredit?> GetProfileCreditAsync(
        ArmRestClient client,
        string profileId,
        string profileName,
        CancellationToken cancellationToken)
    {
        JsonElement summary;
        try
        {
            using JsonDocument document = await client.GetAsync(
                $"{profileId}/providers/Microsoft.Consumption/credits/balanceSummary?api-version={ConsumptionApiVersion}",
                cancellationToken);
            summary = document.RootElement.Clone();
        }
        catch (RequestFailedException ex) when (ex.Status is 400 or 403 or 404)
        {
            return null;
        }

        IReadOnlyList<JsonElement> lots;
        try
        {
            lots = await client.GetAllAsync(
                $"{profileId}/providers/Microsoft.Consumption/lots?api-version={ConsumptionApiVersion}",
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is 400 or 403 or 404)
        {
            lots = [];
        }

        List<CreditLot> creditLots = lots
            .Select(ToCreditLot)
            .OfType<CreditLot>()
            .OrderByDescending(lot => lot.ExpirationDate)
            .ToList();
        if (creditLots.Count == 0)
        {
            return null;
        }

        string currency = JsonValues.GetString(summary, "properties", "balanceSummary", "currentBalance", "currency")
            ?? JsonValues.GetString(summary, "properties", "creditCurrency")
            ?? creditLots[0].Currency;
        decimal current = JsonValues.GetDecimal(summary, "properties", "balanceSummary", "currentBalance", "value");
        decimal estimated = JsonValues.Get(summary, "properties", "balanceSummary", "estimatedBalance", "value") is JsonElement value
            ? JsonValues.ToDecimal(value)
            : current;

        return new BillingProfileCredit(profileId, profileName, currency, current, estimated, creditLots);
    }

    private static CreditLot? ToCreditLot(JsonElement lot)
    {
        DateTimeOffset? start = JsonValues.GetDate(lot, "properties", "startDate");
        DateTimeOffset? expiration = JsonValues.GetDate(lot, "properties", "expirationDate");
        if (start is null || expiration is null)
        {
            return null;
        }

        return new CreditLot(
            JsonValues.GetString(lot, "properties", "source") ?? "Credit",
            JsonValues.GetDecimal(lot, "properties", "originalAmount", "value"),
            JsonValues.GetDecimal(lot, "properties", "closedBalance", "value"),
            JsonValues.GetString(lot, "properties", "originalAmount", "currency") ?? string.Empty,
            start.Value,
            expiration.Value);
    }

    private async Task<(CostHistory? History, string? Error)> GetCostHistoryAsync(
        ArmRestClient client,
        SubscriptionInfo subscription,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _time.GetUtcNow();
        CostHistory? cached = _costCache.Get(subscription.Id);
        if (cached is not null && now - cached.RetrievedAt < CostCacheLifetime)
        {
            return (cached, null);
        }

        try
        {
            CostHistory history = await QueryCostHistoryAsync(client, subscription.Id, now, cancellationToken);
            _costCache.Save(subscription.Id, history);
            return (history, null);
        }
        catch (RequestFailedException ex)
        {
            string error = $"Cost data is not available ({Describe(ex)}).";
            return cached is null
                ? (null, error)
                : (cached, $"Showing costs from {cached.RetrievedAt.LocalDateTime:g}. {error}");
        }
    }

    private static async Task<CostHistory> QueryCostHistoryAsync(
        ArmRestClient client,
        string subscriptionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        DateOnly from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(CostHistoryMonths - 1));
        string body = JsonSerializer.Serialize(new
        {
            type = "ActualCost",
            timeframe = "Custom",
            timePeriod = new
            {
                @from = $"{from:yyyy-MM-dd}T00:00:00Z",
                to = $"{today:yyyy-MM-dd}T23:59:59Z"
            },
            dataset = new
            {
                granularity = "Daily",
                aggregation = new { totalCost = new { name = "Cost", function = "Sum" } }
            }
        });

        Dictionary<DateOnly, decimal> days = [];
        string currency = string.Empty;
        string? url = $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version={CostManagementApiVersion}";

        while (url is not null)
        {
            using JsonDocument document = await client.PostAsync(url, body, cancellationToken);
            JsonElement properties = document.RootElement.GetProperty("properties");

            List<string> columns = properties.GetProperty("columns").EnumerateArray()
                .Select(column => JsonValues.GetString(column, "name") ?? string.Empty)
                .ToList();
            int costIndex = columns.FindIndex(name => name is "Cost" or "PreTaxCost" or "CostUSD");
            int dateIndex = columns.IndexOf("UsageDate");
            int currencyIndex = columns.IndexOf("Currency");

            if (costIndex >= 0 && dateIndex >= 0)
            {
                foreach (JsonElement row in properties.GetProperty("rows").EnumerateArray())
                {
                    if (!TryReadUsageDate(row[dateIndex], out DateOnly date))
                    {
                        continue;
                    }

                    days[date] = days.GetValueOrDefault(date) + JsonValues.ToDecimal(row[costIndex]);
                    if (currencyIndex >= 0 && row[currencyIndex].GetString() is { Length: > 0 } rowCurrency)
                    {
                        currency = rowCurrency;
                    }
                }
            }

            url = JsonValues.GetString(properties, "nextLink");
        }

        List<DailyCost> daily = days
            .OrderBy(day => day.Key)
            .Select(day => new DailyCost(day.Key, day.Value))
            .ToList();
        return new CostHistory(currency, daily, now);
    }

    private static bool TryReadUsageDate(JsonElement value, out DateOnly date)
    {
        string? text = value.ValueKind == JsonValueKind.Number ? value.GetInt64().ToString(CultureInfo.InvariantCulture) : value.GetString();
        return DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static async Task<BillingPeriod?> TryGetBillingPeriodAsync(
        ArmRestClient client,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument document = await client.GetAsync(
                $"/subscriptions/{subscriptionId}/providers/Microsoft.Billing/billingPeriods?api-version={BillingPeriodsApiVersion}&$top=1",
                cancellationToken);

            if (document.RootElement.TryGetProperty("value", out JsonElement periods)
                && periods.ValueKind == JsonValueKind.Array
                && periods.GetArrayLength() > 0
                && DateOnly.TryParseExact(JsonValues.GetString(periods[0], "properties", "billingPeriodStartDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly start)
                && DateOnly.TryParseExact(JsonValues.GetString(periods[0], "properties", "billingPeriodEndDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly end))
            {
                return new BillingPeriod(start, end);
            }
        }
        catch (RequestFailedException)
        {
            // Fall back to the calendar month.
        }

        return null;
    }

    private static async Task<int?> TryCountResourcesAsync(ArmRestClient client, string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<JsonElement> resources = await client.GetAllAsync(
                $"/subscriptions/{subscriptionId}/resources?api-version={ResourcesApiVersion}",
                cancellationToken);
            return resources.Count;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    private static bool IsSignInRequired(Exception exception)
    {
        return exception is AuthenticationFailedException or CredentialUnavailableException;
    }

    private static string Describe(RequestFailedException exception)
    {
        return string.IsNullOrWhiteSpace(exception.ErrorCode) ? $"HTTP {exception.Status}" : exception.ErrorCode;
    }

    private static void AddWarning(List<string> warnings, string warning)
    {
        if (!warnings.Contains(warning))
        {
            warnings.Add(warning);
        }
    }

    private sealed record TenantEntry(string Id, string Name);
}
