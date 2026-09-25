using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
