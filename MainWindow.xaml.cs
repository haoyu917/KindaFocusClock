using System.Windows;
using MyClock.ViewModels;

namespace MyClock;

public partial class MainWindow : Window
{

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}
