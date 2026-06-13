using StorageScanner.ViewModels;
using System.Windows;

namespace StorageScanner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}