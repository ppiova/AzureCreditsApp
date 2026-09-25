using System.Collections.ObjectModel;
using System.Globalization;
using Azure.Identity;
using AzureCreditsApp.Models;
using AzureCreditsApp.Services;

namespace AzureCreditsApp.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string NoValue = "—";

    private readonly IAccountService _accountService;
    private readonly IAzureCreditService _creditService;
    private readonly IMonthlyCreditStore _monthlyCredits;
    private readonly TimeProvider _time;
    private CreditSnapshot? _snapshot;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string? _errorMessage;
    private string _totalCreditRemaining = NoValue;
    private int _attentionCount;
    private string _lastUpdated = string.Empty;
    private SubscriptionCredit? _selectedSubscription;
    private string _monthlyCreditAmount = string.Empty;
    private string _monthlyCreditCurrency = string.Empty;

    public MainWindowViewModel(
        IAccountService accountService,
        IAzureCreditService creditService,
        IMonthlyCreditStore monthlyCredits,
        TimeProvider time)
    {
        _accountService = accountService;
        _creditService = creditService;
        _monthlyCredits = monthlyCredits;
        _time = time;

        AddAccountCommand = new AsyncRelayCommand(AddAccountAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy && Accounts.Count > 0);
        RemoveAccountCommand = new AsyncRelayCommand(parameter => RemoveAccountAsync(parameter as AccountItem), _ => !IsBusy);
        SignInAgainCommand = new AsyncRelayCommand(parameter => SignInAgainAsync(parameter as AccountItem), _ => !IsBusy);
        SaveMonthlyCreditCommand = new AsyncRelayCommand(
            () =>
            {
                SaveMonthlyCredit();
                return Task.CompletedTask;
            },
            () => SelectedSubscription?.UsesMonthlyCredit == true);
    }

    public ObservableCollection<AccountItem> Accounts { get; } = [];

    public ObservableCollection<SubscriptionCredit> Subscriptions { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    public AsyncRelayCommand AddAccountCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RemoveAccountCommand { get; }

    public AsyncRelayCommand SignInAgainCommand { get; }

    public AsyncRelayCommand SaveMonthlyCreditCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                RaiseCommandsChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasWarnings => Warnings.Count > 0;

    public bool HasNoAccounts => Accounts.Count == 0;

    public int SubscriptionCount => Subscriptions.Count;

    /// <summary>
    /// Subscriptions that need the user's attention: high credit usage, expiring
    /// credit, disabled, or billed to a payment method while in use.
    /// </summary>
    public int AttentionCount
    {
        get => _attentionCount;
        private set => SetProperty(ref _attentionCount, value);
    }

    public string TotalCreditRemaining
    {
        get => _totalCreditRemaining;
        private set => SetProperty(ref _totalCreditRemaining, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        private set => SetProperty(ref _lastUpdated, value);
    }

    public bool ShowEmptyState => !IsBusy && Subscriptions.Count == 0;

    public string EmptyStateTitle => HasNoAccounts ? "No accounts yet" : "No subscriptions found";

    public string EmptyStateMessage => HasNoAccounts
        ? "Add a Microsoft account to see your Azure subscriptions and remaining credit."
        : "None of the signed-in accounts has access to an Azure subscription.";

    public SubscriptionCredit? SelectedSubscription
    {
        get => _selectedSubscription;
        set
        {
            if (SetProperty(ref _selectedSubscription, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                LoadMonthlyCreditFields(value);
                SaveMonthlyCreditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedSubscription is not null;

    public string MonthlyCreditAmount
    {
        get => _monthlyCreditAmount;
        set => SetProperty(ref _monthlyCreditAmount, value);
    }

    public string MonthlyCreditCurrency
    {
        get => _monthlyCreditCurrency;
        set => SetProperty(ref _monthlyCreditCurrency, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            SetAccounts(await _accountService.LoadAccountsAsync());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load the saved accounts: {ex.Message}";
            return;
        }

        if (Accounts.Count > 0)
        {
            await RefreshAsync();
        }
    }

    public async Task RefreshAsync()
    {
        if (Accounts.Count == 0)
        {
            ApplySnapshot(null);
            return;
        }

        ErrorMessage = null;
        StatusMessage = "Loading subscriptions...";
        IsBusy = true;
        try
        {
            Progress<string> progress = new(message => StatusMessage = message);
            CreditSnapshot snapshot = await _creditService.LoadAsync(Accounts.Select(item => item.Account).ToList(), progress);
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load subscriptions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Parses an amount typed with either decimal separator: "150.5", "150,5",
    /// "1,234.56" and "1.234,56" all work regardless of the Windows region.
    /// </summary>
    internal static bool TryParseAmount(string text, out decimal amount)
    {
        string value = text.Trim().Replace(" ", string.Empty);
        int lastDot = value.LastIndexOf('.');
        int lastComma = value.LastIndexOf(',');

        char? decimalSeparator;
        if (lastDot >= 0 && lastComma >= 0)
        {
            decimalSeparator = lastDot > lastComma ? '.' : ',';
        }
        else if (lastDot >= 0 || lastComma >= 0)
        {
            char separator = lastDot >= 0 ? '.' : ',';
            int count = value.Count(character => character == separator);
            int digitsAfter = value.Length - value.LastIndexOf(separator) - 1;
            // "1.234" or "1,234" is ambiguous; follow the Windows region in that case.
            decimalSeparator = count > 1
                ? null
                : digitsAfter == 3
                    ? (CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == separator.ToString() ? separator : null)
                    : separator;
        }
        else
        {
            decimalSeparator = null;
        }

        char groupSeparator = decimalSeparator == ',' ? '.' : ',';
        string normalized = value.Replace(groupSeparator.ToString(), string.Empty);
        if (decimalSeparator is char separatorChar)
        {
            normalized = normalized.Replace(separatorChar, '.');
        }
        else
        {
            normalized = normalized.Replace(".", string.Empty).Replace(",", string.Empty);
        }

        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private async Task AddAccountAsync()
    {
        ErrorMessage = null;
        StatusMessage = "Complete the sign-in in your browser...";
        IsBusy = true;
        try
        {
            await _accountService.AddAccountAsync();
            SetAccounts(await _accountService.LoadAccountsAsync());
        }
        catch (Exception ex) when (ex is AuthenticationFailedException or OperationCanceledException)
        {
            ErrorMessage = $"Sign-in did not complete: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync();
    }

    private async Task SignInAgainAsync(AccountItem? item)
    {
        if (item is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = $"Complete the sign-in for {item.Username} in your browser...";
        IsBusy = true;
        try
        {
            await _accountService.ReauthenticateAsync(item.Account);
        }
        catch (Exception ex) when (ex is AuthenticationFailedException or OperationCanceledException)
        {
            ErrorMessage = $"Sign-in did not complete: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync();
    }

    private async Task RemoveAccountAsync(AccountItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _accountService.RemoveAccountAsync(item.Account);
        Accounts.Remove(item);
        OnAccountsChanged();

        // Drop that account's subscriptions without reloading everything from Azure.
        ApplySnapshot(_snapshot is null
            ? null
            : _snapshot with
            {
                Subscriptions = _snapshot.Subscriptions
                    .Where(data => data.Subscription.Account.Id != item.Account.Id)
                    .ToList()
            });
    }

    private void SaveMonthlyCredit()
    {
        if (SelectedSubscription is not SubscriptionCredit selected)
        {
            return;
        }

        string text = MonthlyCreditAmount.Trim();
        if (text.Length == 0)
        {
            _monthlyCredits.Set(selected.Subscription.Id, null);
        }
        else if (!TryParseAmount(text, out decimal amount) || amount <= 0)
        {
            ErrorMessage = "Enter the monthly credit as a positive number, for example 150.";
            return;
        }
        else
        {
            string currency = MonthlyCreditCurrency.Trim().ToUpperInvariant();
            if (currency.Length == 0)
            {
                currency = DefaultCurrency(selected);
            }

            _monthlyCredits.Set(selected.Subscription.Id, new MonthlyCredit(amount, currency));
        }

        ErrorMessage = null;
        Recalculate();
    }

    private void SetAccounts(IReadOnlyList<AccountInfo> accounts)
    {
        Accounts.Clear();
        foreach (AccountInfo account in accounts)
        {
            Accounts.Add(new AccountItem(account));
        }

        OnAccountsChanged();
    }

    private void OnAccountsChanged()
    {
        OnPropertyChanged(nameof(HasNoAccounts));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
        RaiseCommandsChanged();
    }

    private void ApplySnapshot(CreditSnapshot? snapshot)
    {
        _snapshot = snapshot;

        foreach (AccountItem account in Accounts)
        {
            account.NeedsSignIn = snapshot?.AccountsNeedingSignIn.Contains(account.Account.Id) == true;
        }

        Warnings.Clear();
        foreach (string warning in snapshot?.Warnings ?? [])
        {
            Warnings.Add(warning);
        }

        OnPropertyChanged(nameof(HasWarnings));
        LastUpdated = snapshot is null ? string.Empty : $"Updated {snapshot.LoadedAt.LocalDateTime:t}";
        Recalculate();
    }

    private void Recalculate()
    {
        string? selectedId = SelectedSubscription?.Subscription.Id;
        DateTimeOffset now = _time.GetUtcNow();

        List<SubscriptionCredit> credits = (_snapshot?.Subscriptions ?? [])
            .Select(data => CreditCalculator.Calculate(data, _monthlyCredits.Get(data.Subscription.Id), now))
            .OrderByDescending(credit => credit.NeedsAttention)
            .ThenBy(credit => credit.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Subscriptions.Clear();
        foreach (SubscriptionCredit credit in credits)
        {
            Subscriptions.Add(credit);
        }

        TotalCreditRemaining = CreditCalculator.SummarizeRemaining(credits);
        AttentionCount = credits.Count(credit => credit.NeedsAttention);
        OnPropertyChanged(nameof(SubscriptionCount));
        OnPropertyChanged(nameof(ShowEmptyState));

        SelectedSubscription = credits.FirstOrDefault(credit => credit.Subscription.Id == selectedId)
            ?? credits.FirstOrDefault();
    }

    private void LoadMonthlyCreditFields(SubscriptionCredit? credit)
    {
        MonthlyCredit? saved = credit is null ? null : _monthlyCredits.Get(credit.Subscription.Id);
        MonthlyCreditAmount = saved?.Amount.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        MonthlyCreditCurrency = saved?.Currency ?? (credit is null ? string.Empty : DefaultCurrency(credit));
    }

    private static string DefaultCurrency(SubscriptionCredit credit)
    {
        return string.IsNullOrWhiteSpace(credit.Data.Costs?.Currency) ? "USD" : credit.Data.Costs.Currency;
    }

    private void RaiseCommandsChanged()
    {
        AddAccountCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        RemoveAccountCommand.RaiseCanExecuteChanged();
        SignInAgainCommand.RaiseCanExecuteChanged();
    }
}
