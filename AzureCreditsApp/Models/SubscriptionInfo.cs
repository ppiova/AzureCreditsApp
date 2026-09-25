namespace AzureCreditsApp.Models;

public sealed record SubscriptionInfo(
    string Id,
    string Name,
    string State,
    string TenantId,
    string TenantName,
    AccountInfo Account,
    string QuotaId,
    string SpendingLimit,
    OfferKind Offer)
{
    public bool IsEnabled => string.Equals(State, "Enabled", StringComparison.OrdinalIgnoreCase);

    public string PortalUrl => $"https://portal.azure.com/#@{TenantId}/resource/subscriptions/{Id}/overview";
}
