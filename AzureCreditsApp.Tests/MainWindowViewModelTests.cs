using System.ComponentModel;
using AzureCreditsApp.Models;
using AzureCreditsApp.Services;
using AzureCreditsApp.ViewModels;
using Xunit;

namespace AzureCreditsApp.Tests;

public class MainWindowViewModelTests
{
    private readonly FakeAccountService _accounts = new();
    private readonly FakeAzureCreditService _credits = new();
    private readonly InMemoryMonthlyCreditStore _monthlyCredits = new();
    private readonly InMemoryAppSettingsStore _settings = new();

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(_accounts, _credits, _monthlyCredits, _settings, new FixedTimeProvider(TestData.Now));
    }

    [Fact]
    public async Task HidingNotTrackedKeepsTotalsAndRemembersTheChoice()
    {
        _accounts.Saved.Add(TestData.Account);
        _credits.Subscriptions.Add(TestData.Sponsorship());
        _credits.Subscriptions.Add(TestData.Internal("MSFT-ClientCAB-1"));
        _credits.Subscriptions.Add(TestData.Internal("M3CA Shell NonProd"));
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        Assert.Equal(3, viewModel.SubscriptionCount);

        viewModel.HideNotTracked = true;

        Assert.Equal(1, viewModel.SubscriptionCount);
        Assert.Equal(2, viewModel.HiddenCount);
        Assert.Equal("2 hidden", viewModel.HiddenDisplay);
        Assert.Equal(Money.Format(12000m, "USD", compact: true), viewModel.TotalCreditRemaining);
        Assert.True(_settings.Settings.HideNotTracked);
        Assert.True(CreateViewModel().HideNotTracked);
    }

    [Fact]
    public async Task EverythingHiddenExplainsTheFilter()
    {
        _settings.Settings = new AppSettings(HideNotTracked: true);
        _accounts.Saved.Add(TestData.Account);
        _credits.Subscriptions.Add(TestData.Internal());
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.True(viewModel.ShowEmptyState);
        Assert.Contains("hidden by the filter", viewModel.EmptyStateMessage);
        Assert.Null(viewModel.SelectedSubscription);
    }

    [Fact]
    public async Task WithoutAccountsShowsEmptyStateAndDoesNotCallAzure()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.True(viewModel.ShowEmptyState);
        Assert.Equal("No accounts yet", viewModel.EmptyStateTitle);
        Assert.False(viewModel.RefreshCommand.CanExecute(null));
        Assert.Equal(0, _credits.LoadCount);
    }

    [Fact]
    public async Task LoadsSavedAccountsAndSummarizesCredit()
    {
        _accounts.Saved.Add(TestData.Account);
        _credits.Subscriptions.Add(TestData.Sponsorship());
        _credits.Subscriptions.Add(TestData.PayAsYouGo());
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.SubscriptionCount);
        Assert.Equal(1, viewModel.AttentionCount);
        Assert.Equal(Money.Format(12000m, "USD", compact: true), viewModel.TotalCreditRemaining);
        Assert.False(viewModel.ShowEmptyState);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SubscriptionsThatNeedAttentionAreListedFirstAndSelected()
    {
        _accounts.Saved.Add(TestData.Account);
        _credits.Subscriptions.Add(TestData.Sponsorship());
        _credits.Subscriptions.Add(TestData.PayAsYouGo());
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Equal(CreditStatus.BilledToPaymentMethod, viewModel.Subscriptions[0].Status);
        Assert.Same(viewModel.Subscriptions[0], viewModel.SelectedSubscription);
    }

    [Fact]
    public async Task SavingMonthlyCreditRecalculatesWithoutReloading()
    {
        _accounts.Saved.Add(TestData.Account);
        _credits.Subscriptions.Add(TestData.VisualStudio());
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        Assert.Equal("ARS", viewModel.MonthlyCreditCurrency);
        viewModel.MonthlyCreditAmount = "141569,47";
        viewModel.SaveMonthlyCreditCommand.Execute(null);

        SubscriptionCredit selected = Assert.IsType<SubscriptionCredit>(viewModel.SelectedSubscription);
        Assert.Equal(141150.13m, selected.Remaining);
        Assert.Equal(new MonthlyCredit(141569.47m, "ARS"), _monthlyCredits.Get(selected.Subscription.Id));
        Assert.Equal(1, _credits.LoadCount);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task InvalidMonthlyCreditShowsAnError()
    {
        _accounts.Saved.Add(TestData.Account);
        _credits.Subscriptions.Add(TestData.VisualStudio());
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.MonthlyCreditAmount = "abc";
        viewModel.SaveMonthlyCreditCommand.Execute(null);

        Assert.True(viewModel.HasError);
        Assert.Null(_monthlyCredits.Get(viewModel.SelectedSubscription!.Subscription.Id));
    }

    [Fact]
    public async Task AccountsThatNeedSignInAreFlaggedAndWarningsShown()
    {
        _accounts.Saved.Add(TestData.Account);
        _credits.AccountsNeedingSignIn.Add(TestData.Account.Id);
        _credits.Warnings.Add("someone@outlook.com: sign in again to load its subscriptions.");
        MainWindowViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.True(Assert.Single(viewModel.Accounts).NeedsSignIn);
        Assert.True(viewModel.HasWarnings);
        Assert.Equal("No subscriptions found", viewModel.EmptyStateTitle);
    }

    [Fact]
    public async Task AddingAnAccountLoadsItsSubscriptions()
    {
        _accounts.NextAccountToAdd = TestData.Account;
        _credits.Subscriptions.Add(TestData.Sponsorship());
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.AddAccountCommand, viewModel);

        Assert.Single(viewModel.Accounts);
        Assert.Equal(1, viewModel.SubscriptionCount);
    }

    [Fact]
    public async Task CancelledSignInShowsAnError()
    {
        _accounts.AddAccountError = Errors.SignInCancelled();
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        await ExecuteAsync(viewModel.AddAccountCommand, viewModel);

        Assert.True(viewModel.HasError);
        Assert.Empty(viewModel.Accounts);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RemovingAnAccountDropsItsSubscriptions()
    {
        _accounts.Saved.Add(TestData.Account);
        _accounts.Saved.Add(TestData.OtherAccount);
        _credits.Subscriptions.Add(TestData.Sponsorship());
        _credits.Subscriptions.Add(TestData.Sponsorship(account: TestData.OtherAccount) with
        {
            Subscription = TestData.Subscription("Work", "Sponsored_2016-01-01", account: TestData.OtherAccount)
        });
        MainWindowViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        AccountItem other = viewModel.Accounts.Single(item => item.Account == TestData.OtherAccount);
        viewModel.RemoveAccountCommand.Execute(other);

        Assert.Single(viewModel.Accounts);
        Assert.Equal(1, viewModel.SubscriptionCount);
        Assert.DoesNotContain(TestData.OtherAccount, _accounts.Saved);
    }

    [Fact]
    public async Task ErrorMessageRaisesHasError()
    {
        _accounts.AddAccountError = Errors.SignInCancelled();
        MainWindowViewModel viewModel = CreateViewModel();
        List<string?> changed = RecordChanges(viewModel);

        await ExecuteAsync(viewModel.AddAccountCommand, viewModel);

        Assert.Contains(nameof(MainWindowViewModel.HasError), changed);
    }

    [Theory]
    [InlineData("150", 150)]
    [InlineData("150.5", 150.5)]
    [InlineData("150,5", 150.5)]
    [InlineData("141569.47", 141569.47)]
    [InlineData("141569,47", 141569.47)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1.234.567", 1234567)]
    [InlineData(" 99 ", 99)]
    public void ParsesAmountsWithEitherDecimalSeparator(string text, double expected)
    {
        Assert.True(MainWindowViewModel.TryParseAmount(text, out decimal amount));
        Assert.Equal((decimal)expected, amount);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-5")]
    public void RejectsInvalidAmounts(string text)
    {
        Assert.False(MainWindowViewModel.TryParseAmount(text, out _));
    }

    /// <summary>
    /// AsyncRelayCommand.Execute is async void; wait until the view model is idle again.
    /// </summary>
    private static async Task ExecuteAsync(AsyncRelayCommand command, MainWindowViewModel viewModel)
    {
        command.Execute(null);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while ((viewModel.IsBusy || !command.CanExecute(null)) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        List<string?> changed = [];
        source.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        return changed;
    }
}
