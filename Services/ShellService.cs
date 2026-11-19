using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MyClock.Services;

public sealed class ShellService : IShellService
{
    public bool Confirm(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    public void RevealInExplorer(string filePath)
    {
        if (File.Exists(filePath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true })?.Dispose();
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new IOException($"No folder for {filePath}.");
        }

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true })?.Dispose();
    }
}
