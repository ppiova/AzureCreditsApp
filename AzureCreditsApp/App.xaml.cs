using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AzureCreditsApp.Services;
using AzureCreditsApp.ViewModels;

namespace AzureCreditsApp;

public partial class App : System.Windows.Application
{
    private readonly IServiceProvider _services;

    public App()
    {
        _services = ConfigureServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AccountStore>();
        services.AddSingleton<CostCache>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<IMonthlyCreditStore, MonthlyCreditStore>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<IAzureCreditService, AzureCreditService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
