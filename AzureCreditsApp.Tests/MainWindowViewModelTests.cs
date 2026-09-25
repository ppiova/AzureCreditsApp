using System.ComponentModel;
using AzureCreditsApp.ViewModels;
using Xunit;

namespace AzureCreditsApp.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void StartsInEmptyStateWithoutError()
    {
        MainWindowViewModel viewModel = new();

        Assert.True(viewModel.ShowEmptyState);
        Assert.False(viewModel.HasError);
        Assert.Equal(0, viewModel.SubscriptionCount);
        Assert.Equal(0, viewModel.AttentionCount);
    }

    [Fact]
    public void EmptyStateIsHiddenWhileBusy()
    {
        MainWindowViewModel viewModel = new();
        List<string?> changed = RecordChanges(viewModel);

        viewModel.IsBusy = true;

        Assert.False(viewModel.ShowEmptyState);
        Assert.Contains(nameof(MainWindowViewModel.ShowEmptyState), changed);
    }

    [Fact]
    public void EmptyStateIsHiddenOnceSubscriptionsAreLoaded()
    {
        MainWindowViewModel viewModel = new();
        List<string?> changed = RecordChanges(viewModel);

        viewModel.SubscriptionCount = 3;

        Assert.False(viewModel.ShowEmptyState);
        Assert.Contains(nameof(MainWindowViewModel.ShowEmptyState), changed);
    }

    [Fact]
    public void HasErrorFollowsErrorMessage()
    {
        MainWindowViewModel viewModel = new();
        List<string?> changed = RecordChanges(viewModel);

        viewModel.ErrorMessage = "Sign-in failed.";
        Assert.True(viewModel.HasError);
        Assert.Contains(nameof(MainWindowViewModel.HasError), changed);

        viewModel.ErrorMessage = null;
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void SettingTheSameValueDoesNotRaisePropertyChanged()
    {
        MainWindowViewModel viewModel = new() { SubscriptionCount = 2 };
        List<string?> changed = RecordChanges(viewModel);

        viewModel.SubscriptionCount = 2;

        Assert.Empty(changed);
    }

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        List<string?> changed = [];
        source.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        return changed;
    }
}
