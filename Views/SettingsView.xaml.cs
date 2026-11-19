using System.Windows;
using System.Windows.Controls;
using MyClock.ViewModels;

namespace MyClock.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Refresh the form and the running app list each time the page is shown.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        (DataContext as SettingsViewModel)?.OnPageLoaded();
    }
}
