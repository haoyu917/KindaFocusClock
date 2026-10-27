using Microsoft.Extensions.DependencyInjection;
using MyClock.Services;
using MyClock.ViewModels;
using System.Windows;

namespace MyClock;

public partial class App : Application
{
    
    
    
    
    private ServiceProvider? _serviceProvider;

    
    
    
    
    
    
    private void OnStartup(object sender, StartupEventArgs e)
    {
        
        
        var services = new ServiceCollection();

        
        
        services.AddSingleton<IClockService, ClockService>();
        services.AddSingleton<IStopwatchService, StopwatchService>();
        services.AddSingleton<IWindowTrackingService, WindowTrackingService>();

        
        
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ClockViewModel>();
        services.AddSingleton<StopwatchViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        
        
        
        
        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    
    
    
    
    
    
    protected override void OnExit(ExitEventArgs e)
    {
        
        
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
