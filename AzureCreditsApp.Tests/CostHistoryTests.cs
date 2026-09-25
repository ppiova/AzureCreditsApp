using AzureCreditsApp.Models;
using Xunit;

namespace AzureCreditsApp.Tests;

public class CostHistoryTests
{
    private static readonly CostHistory History = TestData.Costs(
        "ARS",
        (new DateOnly(2026, 8, 31), 10m),
        (new DateOnly(2026, 9, 1), 1.5m),
        (new DateOnly(2026, 9, 24), 2.5m));

    [Fact]
    public void TotalBetweenIncludesBothEnds()
    {
        Assert.Equal(4m, History.TotalBetween(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 24)));
        Assert.Equal(14m, History.TotalBetween(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void MonthlyTotalsAreNewestFirst()
    {
        IReadOnlyList<MonthlyCost> months = History.MonthlyTotals();

        Assert.Equal(2, months.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), months[0].Month);
        Assert.Equal(4m, months[0].Amount);
        Assert.Equal(10m, months[1].Amount);
        Assert.All(months, month => Assert.Equal("ARS", month.Currency));
    }
}
