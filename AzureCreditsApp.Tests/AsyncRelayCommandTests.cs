using AzureCreditsApp.ViewModels;
using Xunit;

namespace AzureCreditsApp.Tests;

public class AsyncRelayCommandTests
{
    [Fact]
    public async Task CannotExecuteAgainWhileRunning()
    {
        TaskCompletionSource release = new();
        int runs = 0;
        AsyncRelayCommand command = new(async () =>
        {
            runs++;
            await release.Task;
        });

        command.Execute(null);
        Assert.False(command.CanExecute(null));

        command.Execute(null);
        Assert.Equal(1, runs);

        // Execute is async void, so wait for the command to report it can run again.
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        command.CanExecuteChanged += (_, _) =>
        {
            if (command.CanExecute(null))
            {
                finished.TrySetResult();
            }
        };

        release.SetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RespectsCanExecutePredicate()
    {
        bool allowed = false;
        AsyncRelayCommand command = new(() => Task.CompletedTask, () => allowed);

        Assert.False(command.CanExecute(null));

        allowed = true;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RaisesCanExecuteChangedAroundExecution()
    {
        int raised = 0;
        AsyncRelayCommand command = new(() => Task.CompletedTask);
        command.CanExecuteChanged += (_, _) => raised++;

        command.Execute(null);

        Assert.Equal(2, raised);
    }
}
