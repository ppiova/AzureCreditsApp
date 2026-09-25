namespace AzureCreditsApp.Models;

/// <summary>
/// The kind of Azure offer behind a subscription, derived from its quota id.
/// It decides where the subscription's credit (if any) comes from.
/// </summary>
public enum OfferKind
{
    Unknown,
    VisualStudio,
    PartnerNetwork,
    Sponsorship,
    FreeTrial,
    AzurePass,
    Students,
    PayAsYouGo,
    DevTest,
    Enterprise,
    Csp,
    Internal
}
