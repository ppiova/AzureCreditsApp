using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using AzureCreditsApp.ViewModels;

namespace AzureCreditsApp;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
