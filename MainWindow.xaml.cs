using System.Windows;

namespace M3UVODDownloader;

public partial class MainWindow : Window
{
    public MainWindow(ViewModels.MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
