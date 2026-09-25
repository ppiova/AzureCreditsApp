using AzureCreditsApp.Models;
using AzureCreditsApp.Services;
using Xunit;

namespace AzureCreditsApp.Tests;

public class CreditCalculatorTests
{
    [Fact]
    public void UnusedSponsorshipIsOnTrack()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.Sponsorship(), null, TestData.Now);

        Assert.Equal(CreditStatus.Healthy, credit.Status);
        Assert.Equal(12000m, credit.Remaining);
        Assert.Equal(12000m, credit.Original);
        Assert.Equal(0d, credit.UsedFraction);
        Assert.Equal(325, credit.DaysLeft);
        Assert.False(credit.NeedsAttention);
        Assert.False(credit.IsEstimate);
    }

    [Fact]
    public void SponsorshipOverEightyPercentUsedIsCritical()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.Sponsorship(remaining: 2000), null, TestData.Now);

        Assert.Equal(CreditStatus.Critical, credit.Status);
        Assert.Equal(Severity.Danger, credit.Severity);
        Assert.True(credit.NeedsAttention);
    }

    [Fact]
    public void SponsorshipOverHalfUsedIsWatched()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.Sponsorship(remaining: 5000), null, TestData.Now);

        Assert.Equal(CreditStatus.Watch, credit.Status);
        Assert.False(credit.NeedsAttention);
    }

    [Fact]
    public void SponsorshipExpiringWithinThirtyDaysNeedsAttention()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.Sponsorship(daysToExpiry: 12), null, TestData.Now);

        Assert.Equal(CreditStatus.Expiring, credit.Status);
        Assert.Equal("Expires in 12 days", credit.StatusText);
        Assert.True(credit.NeedsAttention);
    }

    [Fact]
    public void ExpiredSponsorshipWithCostIsExhausted()
    {
        SubscriptionData data = TestData.Sponsorship(daysToExpiry: -10) with
        {
            Costs = TestData.Costs("USD", (new DateOnly(2026, 9, 20), 35m))
        };

        SubscriptionCredit credit = CreditCalculator.Calculate(data, null, TestData.Now);

        Assert.Equal(CreditStatus.Exhausted, credit.Status);
        Assert.Equal(Severity.Danger, credit.Severity);
        Assert.True(credit.NeedsAttention);
        Assert.Contains("billed to your payment method", credit.Note);
    }

    [Fact]
    public void VisualStudioWithoutMonthlyCreditAsksForIt()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.VisualStudio(), null, TestData.Now);

        Assert.Equal(CreditStatus.NotConfigured, credit.Status);
        Assert.Null(credit.Remaining);
        Assert.Equal(new DateOnly(2026, 10, 1), credit.DueDate);
        Assert.Equal(419.34m, credit.PeriodCost);
        Assert.True(credit.UsesMonthlyCredit);
    }

    [Fact]
    public void VisualStudioEstimateSubtractsOnlyTheCurrentPeriodCost()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(
            TestData.VisualStudio(),
            new MonthlyCredit(141569.47m, "ARS"),
            TestData.Now);

        Assert.Equal(CreditStatus.Healthy, credit.Status);
        Assert.Equal(141150.13m, credit.Remaining);
        Assert.True(credit.IsEstimate);
        Assert.True(credit.IsMonthly);
        Assert.Equal(6, credit.DaysLeft);
    }

    [Fact]
    public void VisualStudioCreditInAnotherCurrencyIsFlagged()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(
            TestData.VisualStudio(),
            new MonthlyCredit(150m, "USD"),
            TestData.Now);

        Assert.Equal(CreditStatus.NotConfigured, credit.Status);
        Assert.Equal("Currency mismatch", credit.StatusText);
        Assert.Null(credit.Remaining);
    }

    [Fact]
    public void VisualStudioCreditUsedUpNeedsAttention()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(
            TestData.VisualStudio(periodCost: 200m, currency: "USD"),
            new MonthlyCredit(150m, "USD"),
            TestData.Now);

        Assert.Equal(CreditStatus.Exhausted, credit.Status);
        Assert.Equal(0m, credit.Remaining);
        Assert.True(credit.NeedsAttention);
        Assert.Contains("spending limit disables", credit.Note);
    }

    [Fact]
    public void PayAsYouGoWithResourcesNeedsAttention()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.PayAsYouGo(), null, TestData.Now);

        Assert.Equal(CreditStatus.BilledToPaymentMethod, credit.Status);
        Assert.Equal(Severity.Danger, credit.Severity);
        Assert.True(credit.NeedsAttention);
        Assert.Contains("29 resources", credit.Note);
    }

    [Fact]
    public void EmptyPayAsYouGoIsNeutral()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.PayAsYouGo(resources: 0, monthCost: 0), null, TestData.Now);

        Assert.Equal(Severity.Neutral, credit.Severity);
        Assert.False(credit.NeedsAttention);
    }

    [Fact]
    public void DisabledSubscriptionNeedsAttention()
    {
        SubscriptionData data = TestData.Sponsorship() with
        {
            Subscription = TestData.Subscription("Old", "Sponsored_2016-01-01", state: "Disabled")
        };

        SubscriptionCredit credit = CreditCalculator.Calculate(data, null, TestData.Now);

        Assert.Equal(CreditStatus.Disabled, credit.Status);
        Assert.True(credit.NeedsAttention);
    }

    [Fact]
    public void InternalSubscriptionsAreNotTracked()
    {
        SubscriptionData data = new(TestData.Subscription("Internal", "Internal_2014-09-01"));

        SubscriptionCredit credit = CreditCalculator.Calculate(data, null, TestData.Now);

        Assert.Equal(CreditStatus.NotTracked, credit.Status);
        Assert.False(credit.NeedsAttention);
    }

    [Fact]
    public void SummaryCountsASharedProfileOnceAndGroupsByCurrency()
    {
        BillingProfileCredit shared = TestData.Profile(12000, 9000, TestData.Now.AddDays(200));
        SubscriptionCredit first = CreditCalculator.Calculate(
            new SubscriptionData(TestData.Subscription("A", "Sponsored_2016-01-01"), shared, 2),
            null,
            TestData.Now);
        SubscriptionCredit second = CreditCalculator.Calculate(
            new SubscriptionData(TestData.Subscription("B", "PayAsYouGo_2014-09-01"), shared, 2),
            null,
            TestData.Now);
        SubscriptionCredit visualStudio = CreditCalculator.Calculate(
            TestData.VisualStudio(),
            new MonthlyCredit(141569.47m, "ARS"),
            TestData.Now);

        string summary = CreditCalculator.SummarizeRemaining([first, second, visualStudio]);

        Assert.Equal($"{Money.Format(141150.13m, "ARS", compact: true)} · {Money.Format(9000m, "USD", compact: true)}", summary);
        Assert.Contains("shared by 2 subscriptions", first.Note);
    }

    [Fact]
    public void PayAsYouGoProjectsTheMonthAndAveragesCompleteMonths()
    {
        SubscriptionData data = TestData.PayAsYouGo() with
        {
            Costs = TestData.Costs(
                "ARS",
                (new DateOnly(2026, 4, 5), 2.57m),
                (new DateOnly(2026, 6, 3), 1.70m),
                (new DateOnly(2026, 8, 20), 1.74m),
                (new DateOnly(2026, 9, 10), 1.39m))
        };

        SubscriptionCredit credit = CreditCalculator.Calculate(data, null, TestData.Now);

        Assert.Equal(1.39m, credit.PeriodCost);
        // 1.39 over 25 of 30 days.
        Assert.Equal(1.67m, credit.ProjectedPeriodCost);
        // April to August is five months, including May and July without cost.
        Assert.Equal(1.20m, credit.AverageMonthlyCost);
        Assert.Equal("ARS", credit.CostCurrency);
    }

    [Fact]
    public void MonthlyCreditProjectsOverTheBillingPeriod()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.VisualStudio(), null, TestData.Now);

        Assert.Equal(419.34m, credit.PeriodCost);
        Assert.Equal(503.21m, credit.ProjectedPeriodCost);
        Assert.Equal(559.28m, credit.AverageMonthlyCost);
        Assert.Contains("by period end", credit.ProjectedDisplay);
    }

    [Fact]
    public void OrganizationSubscriptionsAreNotTrackedAndHaveNoCost()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.Internal(), null, TestData.Now);

        Assert.False(credit.IsTracked);
        Assert.Null(credit.PeriodCost);
        Assert.False(credit.HasProjection);
    }

    [Fact]
    public void SummaryWithoutCreditShowsPlaceholder()
    {
        SubscriptionCredit credit = CreditCalculator.Calculate(TestData.PayAsYouGo(), null, TestData.Now);

        Assert.Equal("—", CreditCalculator.SummarizeRemaining([credit]));
    }
}
