using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace AzureCreditsApp.Services;

/// <summary>
/// Minimal Azure Resource Manager REST client. The billing, consumption and cost
/// endpoints the app needs are not all covered by typed SDKs, so it calls ARM
/// directly with the Azure.Core pipeline (authentication, retries, throttling).
/// </summary>
internal sealed class ArmRestClient
{
    private const string Scope = "https://management.azure.com/.default";
    private static readonly Uri Endpoint = new("https://management.azure.com");

    private readonly HttpPipeline _pipeline;

    public ArmRestClient(TokenCredential credential)
    {
        ArmRestClientOptions options = new()
        {
            RetryPolicy = new RetryPolicy(maxRetries: 5, new ArmThrottlingDelayStrategy())
        };
        options.Diagnostics.ApplicationId = "AzureCreditsApp";

        _pipeline = HttpPipelineBuilder.Build(options, new BearerTokenAuthenticationPolicy(credential, Scope));
    }

    public Task<JsonDocument> GetAsync(string pathOrUrl, CancellationToken cancellationToken)
    {
        return SendAsync(RequestMethod.Get, pathOrUrl, body: null, cancellationToken);
    }

    public Task<JsonDocument> PostAsync(string pathOrUrl, string body, CancellationToken cancellationToken)
    {
        return SendAsync(RequestMethod.Post, pathOrUrl, body, cancellationToken);
    }

    /// <summary>
    /// Reads every page of an ARM list operation (<c>value</c> + <c>nextLink</c>).
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> GetAllAsync(string pathOrUrl, CancellationToken cancellationToken)
    {
        List<JsonElement> items = [];
        string? next = pathOrUrl;

        while (next is not null)
        {
            using JsonDocument page = await GetAsync(next, cancellationToken);
            if (page.RootElement.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in value.EnumerateArray())
                {
                    items.Add(item.Clone());
                }
            }

            next = JsonValues.GetString(page.RootElement, "nextLink");
        }

        return items;
    }

    private async Task<JsonDocument> SendAsync(
        RequestMethod method,
        string pathOrUrl,
        string? body,
        CancellationToken cancellationToken)
    {
        using HttpMessage message = _pipeline.CreateMessage();
        Request request = message.Request;
        request.Method = method;
        request.Uri.Reset(ToUri(pathOrUrl));
        request.Headers.Add("Accept", "application/json");

        if (body is not null)
        {
            request.Headers.Add("Content-Type", "application/json");
            request.Content = RequestContent.Create(Encoding.UTF8.GetBytes(body));
        }

        await _pipeline.SendAsync(message, cancellationToken);

        Response response = message.Response;
        if (response.IsError)
        {
            throw new RequestFailedException(response);
        }

        return response.ContentStream is null
            ? JsonDocument.Parse("{}")
            : await JsonDocument.ParseAsync(response.ContentStream, cancellationToken: cancellationToken);
    }

    private static Uri ToUri(string pathOrUrl)
    {
        return pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? new Uri(pathOrUrl)
            : new Uri(Endpoint, pathOrUrl);
    }

    private sealed class ArmRestClientOptions : ClientOptions
    {
    }
}

/// <summary>
/// Exponential back-off that also honours the Cost Management throttling headers
/// (<c>x-ms-ratelimit-microsoft.costmanagement-*-retry-after</c>, in seconds),
/// which the standard Retry-After handling does not read.
/// </summary>
internal sealed class ArmThrottlingDelayStrategy : DelayStrategy
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);

    public ArmThrottlingDelayStrategy()
        : base(maxDelay: TimeSpan.FromSeconds(60))
    {
    }

    protected override TimeSpan GetNextDelayCore(Response? response, int retryNumber)
    {
        TimeSpan exponential = TimeSpan.FromTicks(BaseDelay.Ticks << Math.Clamp(retryNumber - 1, 0, 5));
        TimeSpan? server = response is null ? null : GetCostManagementRetryAfter(response);
        return server is TimeSpan serverDelay && serverDelay > exponential ? serverDelay : exponential;
    }

    private static TimeSpan? GetCostManagementRetryAfter(Response response)
    {
        TimeSpan? longest = null;
        foreach (HttpHeader header in response.Headers)
        {
            if (header.Name.StartsWith("x-ms-ratelimit-microsoft.costmanagement", StringComparison.OrdinalIgnoreCase)
                && header.Name.EndsWith("retry-after", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(header.Value, out int seconds)
                && (longest is null || seconds > longest.Value.TotalSeconds))
            {
                longest = TimeSpan.FromSeconds(seconds);
            }
        }

        return longest;
    }
}
