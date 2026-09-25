namespace AzureCreditsApp.Models;

/// <summary>
/// A signed-in Microsoft account. <see cref="Id"/> is the MSAL home account id.
/// </summary>
public sealed record AccountInfo(string Id, string Username);
