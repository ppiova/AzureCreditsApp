namespace AzureCreditsApp.Models;

public sealed record DailyCost(DateOnly Date, decimal Amount);

public sealed record MonthlyCost(DateOnly Month, decimal Amount, string Currency)
{
    public string MonthDisplay => Month.ToString("MMM yyyy");

    public string AmountDisplay => Money.Format(Amount, Currency);
}

/// <summary>
/// Actual (pre-credit) daily cost of a subscription as reported by Cost Management.
/// </summary>
public sealed record CostHistory(string Currency, IReadOnlyList<DailyCost> Days, DateTimeOffset RetrievedAt)
{
    public decimal TotalBetween(DateOnly from, DateOnly to)
    {
        return Days.Where(day => day.Date >= from && day.Date <= to).Sum(day => day.Amount);
    }

    public IReadOnlyList<MonthlyCost> MonthlyTotals()
    {
        return Days
            .GroupBy(day => new DateOnly(day.Date.Year, day.Date.Month, 1))
            .OrderByDescending(month => month.Key)
            .Select(month => new MonthlyCost(month.Key, month.Sum(day => day.Amount), Currency))
            .ToList();
    }
}

public readonly record struct BillingPeriod(DateOnly Start, DateOnly End);
