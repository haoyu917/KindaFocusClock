using System.Windows.Input;
using MyClock.Commands;

namespace MyClock.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentViewModel;

    public MainViewModel(
        ClockViewModel clockViewModel,
        StopwatchViewModel stopwatchViewModel,
        SettingsViewModel settingsViewModel)
    {

        ClockViewModel = clockViewModel;
        StopwatchViewModel = stopwatchViewModel;
        SettingsViewModel = settingsViewModel;
        _currentViewModel = clockViewModel;
        ShowClockCommand = new RelayCommand(ShowClock);
        ShowStopwatchCommand = new RelayCommand(ShowStopwatch);
        ShowSettingsCommand = new RelayCommand(ShowSettings);
    }

    public ClockViewModel ClockViewModel { get; }

    public StopwatchViewModel StopwatchViewModel { get; }

    public SettingsViewModel SettingsViewModel { get; }

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public ICommand ShowClockCommand { get; }

    public ICommand ShowStopwatchCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    private void ShowClock()
    {

        NavigateTo(ClockViewModel);
    }

    private void ShowStopwatch()
    {

        NavigateTo(StopwatchViewModel);
    }

    private void ShowSettings()
    {

        NavigateTo(SettingsViewModel);
    }

    private void NavigateTo(ViewModelBase viewModel)
    {

        if (ReferenceEquals(CurrentViewModel, viewModel))
        {
            return;
        }

        CurrentViewModel = viewModel;
    }
}
