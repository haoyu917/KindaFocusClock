using System.Windows;
using System.Windows.Controls;
using MyClock.ViewModels;

namespace MyClock.Views;

public partial class StopwatchView : UserControl
{

    public StopwatchView()
    {

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {

        (DataContext as StopwatchViewModel)?.OnPageLoaded();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {

        (DataContext as StopwatchViewModel)?.OnPageUnloaded();
    }
}
