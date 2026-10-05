using AzureCreditsApp.Models;

namespace AzureCreditsApp.ViewModels;

public sealed class AccountItem : ObservableObject
{
    private bool _needsSignIn;

    public AccountItem(AccountInfo account)
    {
        Account = account;
    }

    public AccountInfo Account { get; }

    public string Username => Account.Username;

    public bool NeedsSignIn
    {
        get => _needsSignIn;
        set => SetProperty(ref _needsSignIn, value);
    }
}
