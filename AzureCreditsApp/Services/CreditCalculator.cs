using AzureCreditsApp.Models;

namespace AzureCreditsApp.Services;

/// <summary>
/// Turns the raw data loaded from Azure into a credit status per subscription.
/// Pure logic with no Azure calls, so it can be re-run when settings change.
/// </summary>
public static class CreditCalculator
{
    public const double WatchThreshold = 0.5;
    public const double CriticalThreshold = 0.8;
    public const int ExpiringSoonDays = 30;

    public static SubscriptionCredit Calculate(SubscriptionData data, MonthlyCredit? monthlyCredit, DateTimeOffset now)
    {
        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        return AddCostSummary(CalculateStatus(data, monthlyCredit, now, today), data, today);
    }

    private static SubscriptionCredit CalculateStatus(SubscriptionData data, MonthlyCredit? monthlyCredit, DateTimeOffset now, DateOnly today)
    {
        SubscriptionInfo subscription = data.Subscription;

        if (!subscription.IsEnabled)
        {
            return new SubscriptionCredit(
                data,
                CreditStatus.Disabled,
                Severity.Danger,
                $"Subscription {subscription.State.ToLowerInvariant()}",
                "The subscription is not active. Check its status in the Azure portal.",
                NeedsAttention: true);
        }

        if (data.BillingProfile is BillingProfileCredit profile)
        {
            return FromBillingProfile(data, profile, now, today);
        }

        if (OfferClassifier.HasMonthlyCredit(subscription.Offer))
        {
            return FromMonthlyCredit(data, monthlyCredit, today);
        }

        if (OfferClassifier.IsBilledToPaymentMethod(subscription.Offer))
        {
            return FromPaymentMethod(data, today);
        }

        string note = subscription.Offer == OfferKind.Sponsorship
            ? "This sponsorship is not on a Microsoft Customer Agreement billing profile, so its balance is only shown at microsoftazuresponsor.com."
            : "Credit tracking is not available for this offer.";
        return new SubscriptionCredit(data, CreditStatus.NotTracked, Severity.Neutral, "Not tracked", note, NeedsAttention: false);
    }

    /// <summary>
    /// Total remaining credit per currency. A billing profile shared by several
    /// subscriptions is counted once.
    /// </summary>
    public static string SummarizeRemaining(IEnumerable<SubscriptionCredit> credits)
    {
        IEnumerable<string> totals = credits
            .Where(credit => credit.Remaining is > 0m && !string.IsNullOrWhiteSpace(credit.Currency))
            .GroupBy(credit => credit.Data.BillingProfile?.Id ?? credit.Subscription.Id, StringComparer.OrdinalIgnoreCase)
            .Select(pool => pool.First())
            .GroupBy(credit => credit.Currency!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(currency => currency.Key, StringComparer.Ordinal)
            .Select(currency => Money.Format(currency.Sum(credit => credit.Remaining!.Value), currency.Key, compact: true));

        string summary = string.Join(" · ", totals);
        return summary.Length == 0 ? "—" : summary;
    }

    private static SubscriptionCredit FromBillingProfile(
        SubscriptionData data,
        BillingProfileCredit profile,
        DateTimeOffset now,
        DateOnly today)
    {
        List<CreditLot> activeLots = profile.Lots.Where(lot => lot.IsActiveAt(now)).ToList();
        decimal monthCost = MonthToDateCost(data, today);
        string shared = data.SubscriptionsSharingProfile > 1
            ? $" The credit is shared by {data.SubscriptionsSharingProfile} subscriptions on billing profile '{profile.DisplayName}'."
            : string.Empty;

        if (activeLots.Count == 0)
        {
            DateOnly lastExpiration = DateOnly.FromDateTime(profile.Lots.Max(lot => lot.ExpirationDate).LocalDateTime);
            bool inUse = monthCost > 0 || (data.ResourceCount ?? 0) > 0;
            return new SubscriptionCredit(
                data,
                CreditStatus.Exhausted,
                inUse ? Severity.Danger : Severity.Warning,
                "No active credit",
                $"The last credit expired on {lastExpiration:MMM d, yyyy}. Usage is now billed to your payment method.{shared}",
                NeedsAttention: inUse,
                Currency: profile.Currency,
                Remaining: 0m,
                PeriodCost: monthCost);
        }

        decimal original = activeLots.Sum(lot => lot.OriginalAmount);
        decimal remaining = Math.Max(0m, profile.EstimatedBalance);
        double used = original > 0 ? (double)Math.Clamp(1 - remaining / original, 0m, 1m) : 1;
        DateOnly expires = DateOnly.FromDateTime(activeLots.Min(lot => lot.ExpirationDate).LocalDateTime);
        int daysLeft = expires.DayNumber - today.DayNumber;

        (CreditStatus status, Severity severity, string text, bool attention) = Classify(remaining, used, daysLeft, expires: true);
        string note = status switch
        {
            CreditStatus.Exhausted => "The credit is used up. Usage is billed to your payment method.",
            CreditStatus.Critical => "Most of the credit is used. When it runs out, usage is billed to your payment method.",
            CreditStatus.Expiring => "Unused credit is lost when it expires.",
            CreditStatus.Watch => "More than half of the credit is used.",
            _ => $"Credit available until {expires:MMM d, yyyy}."
        };

        return new SubscriptionCredit(
            data,
            status,
            severity,
            text,
            note + shared,
            attention,
            profile.Currency,
            remaining,
            original,
            used,
            expires,
            daysLeft,
            PeriodCost: monthCost);
    }

    private static SubscriptionCredit FromMonthlyCredit(SubscriptionData data, MonthlyCredit? monthlyCredit, DateOnly today)
    {
        BillingPeriod period = data.BillingPeriod ?? CalendarMonth(today);
        DateOnly resets = period.End.AddDays(1);
        int daysLeft = resets.DayNumber - today.DayNumber;
        decimal? periodCost = data.Costs?.TotalBetween(period.Start, today);
        string? costCurrency = string.IsNullOrWhiteSpace(data.Costs?.Currency) ? null : data.Costs.Currency;

        if (monthlyCredit is null)
        {
            return new SubscriptionCredit(
                data,
                CreditStatus.NotConfigured,
                Severity.Neutral,
                "Set monthly credit",
                "The Visual Studio credit has no public API. Enter your monthly credit amount to estimate what is left.",
                NeedsAttention: false,
                Currency: costCurrency,
                DueDate: resets,
                DaysLeft: daysLeft,
                IsMonthly: true,
                PeriodCost: periodCost);
        }

        if (periodCost is not decimal cost)
        {
            return new SubscriptionCredit(
                data,
                CreditStatus.NotTracked,
                Severity.Neutral,
                "No cost data",
                "Cost data is not available yet, so the remaining credit cannot be estimated.",
                NeedsAttention: false,
                Currency: monthlyCredit.Currency,
                Original: monthlyCredit.Amount,
                DueDate: resets,
                DaysLeft: daysLeft,
                IsMonthly: true);
        }

        if (costCurrency is not null && !string.Equals(costCurrency, monthlyCredit.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return new SubscriptionCredit(
                data,
                CreditStatus.NotConfigured,
                Severity.Warning,
                "Currency mismatch",
                $"Costs are billed in {costCurrency} but the monthly credit is in {monthlyCredit.Currency}. Enter the credit in {costCurrency}.",
                NeedsAttention: false,
                Currency: costCurrency,
                DueDate: resets,
                DaysLeft: daysLeft,
                IsMonthly: true,
                PeriodCost: cost);
        }

        decimal remaining = monthlyCredit.Amount - cost;
        double used = monthlyCredit.Amount > 0 ? (double)Math.Clamp(cost / monthlyCredit.Amount, 0m, 1m) : 1;
        (CreditStatus status, Severity severity, string text, bool attention) = Classify(remaining, used, daysLeft, expires: false);
        string limit = string.Equals(data.Subscription.SpendingLimit, "On", StringComparison.OrdinalIgnoreCase)
            ? "the spending limit disables the subscription until the credit resets"
            : "the spending limit is off, so usage beyond the credit is billed to your payment method";
        string note = status switch
        {
            CreditStatus.Exhausted => $"The monthly credit is used up: {limit}.",
            CreditStatus.Critical => $"Close to the monthly credit. When it runs out, {limit}.",
            _ => "Estimated from the monthly amount you entered minus this period's cost."
        };

        return new SubscriptionCredit(
            data,
            status,
            severity,
            text,
            note,
            attention,
            monthlyCredit.Currency,
            Math.Max(0m, remaining),
            monthlyCredit.Amount,
            used,
            resets,
            daysLeft,
            IsMonthly: true,
            IsEstimate: true,
            PeriodCost: cost);
    }

    private static SubscriptionCredit FromPaymentMethod(SubscriptionData data, DateOnly today)
    {
        decimal monthCost = MonthToDateCost(data, today);
        int resources = data.ResourceCount ?? 0;
        bool inUse = resources > 0 || monthCost > 0;
        string? currency = string.IsNullOrWhiteSpace(data.Costs?.Currency) ? null : data.Costs.Currency;

        string note = inUse
            ? $"No credit: usage is billed to your payment method. It has {resources} {(resources == 1 ? "resource" : "resources")} and {Money.Format(monthCost, currency)} of cost this month."
            : "No credit: any usage is billed to your payment method.";

        return new SubscriptionCredit(
            data,
            CreditStatus.BilledToPaymentMethod,
            inUse ? Severity.Danger : Severity.Neutral,
            "Billed to payment method",
            note,
            NeedsAttention: inUse,
            Currency: currency,
            PeriodCost: monthCost);
    }

    private static (CreditStatus Status, Severity Severity, string Text, bool NeedsAttention) Classify(
        decimal remaining,
        double used,
        int daysLeft,
        bool expires)
    {
        if (remaining <= 0)
        {
            return (CreditStatus.Exhausted, Severity.Danger, "Credit used up", true);
        }

        if (used >= CriticalThreshold)
        {
            return (CreditStatus.Critical, Severity.Danger, $"{used:P0} used", true);
        }

        if (expires && daysLeft <= ExpiringSoonDays)
        {
            return (CreditStatus.Expiring, Severity.Warning, daysLeft <= 0 ? "Expires today" : $"Expires in {daysLeft} days", true);
        }

        if (used >= WatchThreshold)
        {
            return (CreditStatus.Watch, Severity.Warning, $"{used:P0} used", false);
        }

        return (CreditStatus.Healthy, Severity.Good, "On track", false);
    }

    /// <summary>
    /// Adds the cost of the current period (billing period for monthly credits,
    /// calendar month otherwise), a straight-line projection to the end of that
    /// period, and the average of the previous complete months.
    /// </summary>
    private static SubscriptionCredit AddCostSummary(SubscriptionCredit credit, SubscriptionData data, DateOnly today)
    {
        if (data.Costs is not CostHistory costs)
        {
            return credit;
        }

        BillingPeriod period = credit.IsMonthly ? data.BillingPeriod ?? CalendarMonth(today) : CalendarMonth(today);
        decimal periodCost = costs.TotalBetween(period.Start, today);
        int elapsedDays = today.DayNumber - period.Start.DayNumber + 1;
        int periodDays = period.End.DayNumber - period.Start.DayNumber + 1;
        decimal projected = elapsedDays > 0 ? Math.Round(periodCost / elapsedDays * periodDays, 2) : periodCost;

        // Months without any cost have no rows, so count calendar months from the first
        // month with data up to the last complete month.
        DateOnly currentMonth = new(today.Year, today.Month, 1);
        Dictionary<DateOnly, decimal> pastMonths = costs.MonthlyTotals()
            .Where(month => month.Month < currentMonth)
            .ToDictionary(month => month.Month, month => month.Amount);
        decimal? average = null;
        if (pastMonths.Count > 0)
        {
            DateOnly first = pastMonths.Keys.Min();
            int months = (currentMonth.Year - first.Year) * 12 + currentMonth.Month - first.Month;
            average = Math.Round(pastMonths.Values.Sum() / months, 2);
        }

        return credit with
        {
            PeriodCost = periodCost,
            ProjectedPeriodCost = projected,
            AverageMonthlyCost = average
        };
    }

    private static decimal MonthToDateCost(SubscriptionData data, DateOnly today)
    {
        return data.Costs?.TotalBetween(new DateOnly(today.Year, today.Month, 1), today) ?? 0m;
    }

    private static BillingPeriod CalendarMonth(DateOnly today)
    {
        DateOnly start = new(today.Year, today.Month, 1);
        return new BillingPeriod(start, start.AddMonths(1).AddDays(-1));
    }
}
