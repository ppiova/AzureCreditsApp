using Azure.Core;
using Azure.Identity;
using AzureCreditsApp.Models;

namespace AzureCreditsApp.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountInfo>> LoadAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the browser so the user can sign in with another Microsoft account.
    /// </summary>
    Task<AccountInfo> AddAccountAsync(CancellationToken cancellationToken = default);

    Task ReauthenticateAsync(AccountInfo account, CancellationToken cancellationToken = default);

    Task RemoveAccountAsync(AccountInfo account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Credential for silent token requests in a tenant (or any organization when
    /// <paramref name="tenantId"/> is null). It never opens a browser: when the user
    /// has to interact, token requests throw <see cref="AuthenticationRequiredException"/>.
    /// </summary>
    TokenCredential GetCredential(AccountInfo account, string? tenantId = null);
}

public sealed class AccountService : IAccountService
{
    private const string AnyOrganization = "organizations";
    private static readonly string[] ArmScopes = ["https://management.azure.com/.default"];
    private static readonly TokenCachePersistenceOptions TokenCache = new() { Name = "AzureCreditsApp" };

    private readonly AccountStore _store;
    private readonly Dictionary<string, AuthenticationRecord> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TokenCredential> _credentials = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public AccountService(AccountStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<AccountInfo>> LoadAccountsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuthenticationRecord> records = await _store.LoadAsync(cancellationToken);

        lock (_gate)
        {
            _records.Clear();
            _credentials.Clear();
            foreach (AuthenticationRecord record in records)
            {
                _records[record.HomeAccountId] = record;
            }

            return _records.Values
                .Select(ToAccount)
                .OrderBy(account => account.Username, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public async Task<AccountInfo> AddAccountAsync(CancellationToken cancellationToken = default)
    {
        InteractiveBrowserCredential credential = new(CreateOptions(AnyOrganization, record: null, allowInteraction: true));
        AuthenticationRecord record = await credential.AuthenticateAsync(new TokenRequestContext(ArmScopes), cancellationToken);

        await RememberAsync(record, cancellationToken);
        return ToAccount(record);
    }

    public async Task ReauthenticateAsync(AccountInfo account, CancellationToken cancellationToken = default)
    {
        InteractiveBrowserCredentialOptions options = CreateOptions(AnyOrganization, record: null, allowInteraction: true);
        options.LoginHint = account.Username;

        AuthenticationRecord record = await new InteractiveBrowserCredential(options)
            .AuthenticateAsync(new TokenRequestContext(ArmScopes), cancellationToken);

        await RememberAsync(record, cancellationToken);
    }

    public Task RemoveAccountAsync(AccountInfo account, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _records.Remove(account.Id);
            ForgetCredentials(account.Id);
        }

        _store.Delete(account.Id);
        return Task.CompletedTask;
    }

    public TokenCredential GetCredential(AccountInfo account, string? tenantId = null)
    {
        string tenant = tenantId ?? AnyOrganization;
        string key = $"{account.Id}|{tenant}";

        lock (_gate)
        {
            if (_credentials.TryGetValue(key, out TokenCredential? existing))
            {
                return existing;
            }

            if (!_records.TryGetValue(account.Id, out AuthenticationRecord? record))
            {
                throw new InvalidOperationException($"{account.Username} is not signed in.");
            }

            InteractiveBrowserCredential credential = new(CreateOptions(tenant, record, allowInteraction: false));
            _credentials[key] = credential;
            return credential;
        }
    }

    private async Task RememberAsync(AuthenticationRecord record, CancellationToken cancellationToken)
    {
        await _store.SaveAsync(record, cancellationToken);

        lock (_gate)
        {
            _records[record.HomeAccountId] = record;
            ForgetCredentials(record.HomeAccountId);
        }
    }

    private void ForgetCredentials(string accountId)
    {
        foreach (string key in _credentials.Keys.Where(key => key.StartsWith($"{accountId}|", StringComparison.Ordinal)).ToList())
        {
            _credentials.Remove(key);
        }
    }

    private static InteractiveBrowserCredentialOptions CreateOptions(string tenantId, AuthenticationRecord? record, bool allowInteraction)
    {
        InteractiveBrowserCredentialOptions options = new()
        {
            TenantId = tenantId,
            AuthenticationRecord = record,
            TokenCachePersistenceOptions = TokenCache,
            DisableAutomaticAuthentication = !allowInteraction
        };

        // Guest directories are reached with the same account, so allow any tenant.
        options.AdditionallyAllowedTenants.Add("*");
        return options;
    }

    private static AccountInfo ToAccount(AuthenticationRecord record)
    {
        return new AccountInfo(record.HomeAccountId, record.Username);
    }
}
