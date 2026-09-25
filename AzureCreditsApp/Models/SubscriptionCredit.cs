using AzureCreditsApp.Services;

namespace AzureCreditsApp.Models;

public enum CreditStatus
{
    Healthy,
    Watch,
    Critical,
    Expiring,
    Exhausted,
    BilledToPaymentMethod,
    NotConfigured,
    NotTracked,
    Disabled
}

public enum Severity
{
    Neutral,
    Good,
    Warning,
    Danger
}

/// <summary>
/// Credit status of a subscription, ready to display.
/// </summary>
public sealed record SubscriptionCredit(
    SubscriptionData Data,
    CreditStatus Status,
    Severity Severity,
    string StatusText,
    string Note,
    bool NeedsAttention,
    string? Currency = null,
    decimal? Remaining = null,
    decimal? Original = null,
    double? UsedFraction = null,
    DateOnly? DueDate = null,
    int? DaysLeft = null,
    bool IsMonthly = false,
    bool IsEstimate = false,
    decimal? PeriodCost = null)
{
    private const string NoValue = "—";

    public SubscriptionInfo Subscription => Data.Subscription;

    public string Name => Subscription.Name;

    public string AccountName => Subscription.Account.Username;

    public string OfferDisplay => OfferClassifier.DisplayName(Subscription.Offer);

    public bool HasRemaining => Remaining is not null;

    public string RemainingDisplay => Remaining is decimal remaining
        ? (IsEstimate ? "≈ " : string.Empty) + Money.Format(remaining, Currency)
        : NoValue;

    public string OriginalDisplay => Original is decimal original
        ? $"of {Money.Format(original, Currency)}{(IsMonthly ? " a month" : string.Empty)}"
        : string.Empty;

    public bool HasUsage => UsedFraction is not null;

    public double UsedPercent => Math.Round((UsedFraction ?? 0) * 100, 1);

    public string UsedDisplay => UsedFraction is double used ? $"{used:P0}" : NoValue;

    public bool HasDueDate => DueDate is not null;

    public string DueLabel => IsMonthly ? "Resets" : "Expires";

    public string DueDateDisplay => DueDate is DateOnly due ? due.ToString("MMM d, yyyy") : NoValue;

    public string DueHint => DaysLeft switch
    {
        null => string.Empty,
        < 0 => $"{-DaysLeft} days ago",
        0 => "today",
        1 => IsMonthly ? "in 1 day" : "1 day left",
        int days => IsMonthly ? $"in {days} days" : $"{days} days left"
    };

    public string PeriodCostLabel => IsMonthly ? "Cost this period" : "Cost this month";

    public string PeriodCostDisplay => PeriodCost is decimal cost
        ? Money.Format(cost, string.IsNullOrEmpty(Data.Costs?.Currency) ? Currency : Data.Costs.Currency)
        : NoValue;

    public IReadOnlyList<CreditLot> Lots => Data.BillingProfile?.Lots ?? [];

    public bool HasLots => Lots.Count > 0;

    public IReadOnlyList<MonthlyCost> MonthlyCosts => Data.Costs?.MonthlyTotals() ?? [];

    public string? CostError => Data.CostError;

    public bool HasCostError => !string.IsNullOrWhiteSpace(Data.CostError);

    public bool UsesMonthlyCredit => OfferClassifier.HasMonthlyCredit(Subscription.Offer)
        && Data.BillingProfile is null;
}
