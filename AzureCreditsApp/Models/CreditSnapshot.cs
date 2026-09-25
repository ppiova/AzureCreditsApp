namespace AzureCreditsApp.Models;

/// <summary>
/// Raw data loaded from Azure for one subscription. Credit status is computed
/// from it by <c>CreditCalculator</c>, so it can be recalculated without calling
/// Azure again (for example after the user edits a monthly credit amount).
/// </summary>
public sealed record SubscriptionData(
    SubscriptionInfo Subscription,
    BillingProfileCredit? BillingProfile = null,
    int SubscriptionsSharingProfile = 0,
    CostHistory? Costs = null,
    string? CostError = null,
    BillingPeriod? BillingPeriod = null,
    int? ResourceCount = null);

public sealed record CreditSnapshot(
    IReadOnlyList<SubscriptionData> Subscriptions,
    IReadOnlyList<string> Warnings,
    IReadOnlySet<string> AccountsNeedingSignIn,
    DateTimeOffset LoadedAt);

/// <summary>
/// Monthly credit amount the user entered for an offer that has no credit API
/// (Visual Studio, partner network).
/// </summary>
public sealed record MonthlyCredit(decimal Amount, string Currency);
