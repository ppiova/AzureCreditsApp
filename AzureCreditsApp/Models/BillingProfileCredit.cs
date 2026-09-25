namespace AzureCreditsApp.Models;

/// <summary>
/// A credit lot on a Microsoft Customer Agreement billing profile, for example
/// an Azure Sponsorship grant. <see cref="RemainingAmount"/> is the balance as of
/// the last invoice, so it can lag behind the profile's current balance.
/// </summary>
public sealed record CreditLot(
    string Source,
    decimal OriginalAmount,
    decimal RemainingAmount,
    string Currency,
    DateTimeOffset StartDate,
    DateTimeOffset ExpirationDate)
{
    public bool IsActiveAt(DateTimeOffset now) => ExpirationDate > now;

    public string AmountDisplay => $"{Money.Format(RemainingAmount, Currency)} of {Money.Format(OriginalAmount, Currency)}";

    public string PeriodDisplay => $"{StartDate.LocalDateTime:d} → {ExpirationDate.LocalDateTime:d}";
}

/// <summary>
/// Credit held by a billing profile. The credit is shared by every subscription
/// billed to the profile, not owned by a single subscription.
/// </summary>
public sealed record BillingProfileCredit(
    string Id,
    string DisplayName,
    string Currency,
    decimal CurrentBalance,
    decimal EstimatedBalance,
    IReadOnlyList<CreditLot> Lots);
