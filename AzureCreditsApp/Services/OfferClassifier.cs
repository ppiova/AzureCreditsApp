using AzureCreditsApp.Models;

namespace AzureCreditsApp.Services;

public static class OfferClassifier
{
    /// <summary>
    /// Maps a subscription quota id (for example <c>MSDN_2014-09-01</c>) to its offer kind.
    /// The display name of a subscription is not reliable: a sponsorship keeps its name
    /// after it is converted to Pay-As-You-Go, but its quota id changes.
    /// </summary>
    public static OfferKind Classify(string? quotaId)
    {
        if (string.IsNullOrWhiteSpace(quotaId))
        {
            return OfferKind.Unknown;
        }

        string family = quotaId.Split('_')[0].ToUpperInvariant();
        return family switch
        {
            "MSDN" => OfferKind.VisualStudio,
            "MSDNDEVTEST" => OfferKind.DevTest,
            "MPN" => OfferKind.PartnerNetwork,
            "SPONSORED" => OfferKind.Sponsorship,
            "FREETRIAL" => OfferKind.FreeTrial,
            "AZUREPASS" => OfferKind.AzurePass,
            "AZUREFORSTUDENTS" or "AZUREFORSTUDENTSFREE" or "DREAMSPARK" => OfferKind.Students,
            "PAYASYOUGO" => OfferKind.PayAsYouGo,
            "ENTERPRISEAGREEMENT" or "ENTERPRISEAGREEMENTDEVTEST" => OfferKind.Enterprise,
            "CSP" => OfferKind.Csp,
            "INTERNAL" => OfferKind.Internal,
            _ => OfferKind.Unknown
        };
    }

    public static string DisplayName(OfferKind kind) => kind switch
    {
        OfferKind.VisualStudio => "Visual Studio",
        OfferKind.PartnerNetwork => "Partner network",
        OfferKind.Sponsorship => "Azure Sponsorship",
        OfferKind.FreeTrial => "Free trial",
        OfferKind.AzurePass => "Azure Pass",
        OfferKind.Students => "Azure for Students",
        OfferKind.PayAsYouGo => "Pay-As-You-Go",
        OfferKind.DevTest => "Pay-As-You-Go Dev/Test",
        OfferKind.Enterprise => "Enterprise Agreement",
        OfferKind.Csp => "CSP",
        OfferKind.Internal => "Microsoft internal",
        _ => "Other"
    };

    /// <summary>
    /// Offers with a monthly credit that has no public API, so the user enters the amount.
    /// </summary>
    public static bool HasMonthlyCredit(OfferKind kind) => kind is OfferKind.VisualStudio or OfferKind.PartnerNetwork;

    /// <summary>
    /// Offers without credit, where every charge goes to the payment method on file.
    /// </summary>
    public static bool IsBilledToPaymentMethod(OfferKind kind) => kind is OfferKind.PayAsYouGo or OfferKind.DevTest;

    /// <summary>
    /// Offers whose cost is worth querying. Enterprise, CSP and internal subscriptions
    /// are billed by an organization, so the app does not track them.
    /// </summary>
    public static bool TracksCost(OfferKind kind) => kind is not (OfferKind.Enterprise or OfferKind.Csp or OfferKind.Internal or OfferKind.Unknown);
}
