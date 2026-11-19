using System.Windows;
using System.Windows.Controls;
using MyClock.ViewModels;

namespace MyClock.Views;

public partial class SummaryView : UserControl
{
    public SummaryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Reload each time the page is shown so the latest sessions appear.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        (DataContext as SummaryViewModel)?.OnPageLoaded();
    }
}
