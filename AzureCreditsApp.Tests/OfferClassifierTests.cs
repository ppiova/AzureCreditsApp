using AzureCreditsApp.Models;
using AzureCreditsApp.Services;
using Xunit;

namespace AzureCreditsApp.Tests;

public class OfferClassifierTests
{
    [Theory]
    [InlineData("MSDN_2014-09-01", OfferKind.VisualStudio)]
    [InlineData("MSDNDevTest_2014-09-01", OfferKind.DevTest)]
    [InlineData("Sponsored_2016-01-01", OfferKind.Sponsorship)]
    [InlineData("PayAsYouGo_2014-09-01", OfferKind.PayAsYouGo)]
    [InlineData("FreeTrial_2014-09-01", OfferKind.FreeTrial)]
    [InlineData("AzureForStudents_2018-01-01", OfferKind.Students)]
    [InlineData("MPN_2014-09-01", OfferKind.PartnerNetwork)]
    [InlineData("EnterpriseAgreement_2014-09-01", OfferKind.Enterprise)]
    [InlineData("CSP_2015-05-01", OfferKind.Csp)]
    [InlineData("Internal_2014-09-01", OfferKind.Internal)]
    [InlineData("msdn_2014-09-01", OfferKind.VisualStudio)]
    [InlineData("Something_2030-01-01", OfferKind.Unknown)]
    [InlineData("", OfferKind.Unknown)]
    [InlineData(null, OfferKind.Unknown)]
    public void ClassifiesQuotaIds(string? quotaId, OfferKind expected)
    {
        Assert.Equal(expected, OfferClassifier.Classify(quotaId));
    }

    [Fact]
    public void OnlyMonthlyCreditOffersNeedAManualAmount()
    {
        Assert.True(OfferClassifier.HasMonthlyCredit(OfferKind.VisualStudio));
        Assert.True(OfferClassifier.HasMonthlyCredit(OfferKind.PartnerNetwork));
        Assert.False(OfferClassifier.HasMonthlyCredit(OfferKind.Sponsorship));
    }

    [Fact]
    public void PayAsYouGoOffersAreBilledToThePaymentMethod()
    {
        Assert.True(OfferClassifier.IsBilledToPaymentMethod(OfferKind.PayAsYouGo));
        Assert.True(OfferClassifier.IsBilledToPaymentMethod(OfferKind.DevTest));
        Assert.False(OfferClassifier.IsBilledToPaymentMethod(OfferKind.VisualStudio));
    }
}
