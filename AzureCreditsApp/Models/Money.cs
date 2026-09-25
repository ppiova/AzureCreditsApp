using System.Globalization;

namespace AzureCreditsApp.Models;

public static class Money
{
    public static string Format(decimal amount, string? currency, bool compact = false)
    {
        string number = amount.ToString(compact ? "N0" : "N2", CultureInfo.CurrentCulture);
        return string.IsNullOrWhiteSpace(currency) ? number : $"{currency} {number}";
    }
}
