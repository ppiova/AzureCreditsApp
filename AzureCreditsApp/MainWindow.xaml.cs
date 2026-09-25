using System.Windows;
using AzureCreditsApp.ViewModels;

namespace AzureCreditsApp;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}
