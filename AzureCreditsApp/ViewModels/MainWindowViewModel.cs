namespace AzureCreditsApp.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string NoValue = "—";

    private bool _isBusy;
    private string _statusMessage = "Add a Microsoft account to see your Azure subscriptions and remaining credit.";
    private string? _errorMessage;
    private int _subscriptionCount;
    private int _attentionCount;
    private string _totalCreditRemaining = NoValue;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public int SubscriptionCount
    {
        get => _subscriptionCount;
        set
        {
            if (SetProperty(ref _subscriptionCount, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    /// <summary>
    /// Subscriptions that need the user's attention: high credit usage,
    /// expiring credit, or Pay-As-You-Go with active resources.
    /// </summary>
    public int AttentionCount
    {
        get => _attentionCount;
        set => SetProperty(ref _attentionCount, value);
    }

    public string TotalCreditRemaining
    {
        get => _totalCreditRemaining;
        set => SetProperty(ref _totalCreditRemaining, value);
    }

    public bool ShowEmptyState => !IsBusy && SubscriptionCount == 0;
}
