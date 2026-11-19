using MyClock.Models;

namespace MyClock.Services;

public interface ISettingsService
{
    // The settings in effect. Replaced, never changed, when settings are saved.
    AppSettings Current { get; }

    // Raised after Save replaces Current.
    event EventHandler? SettingsChanged;

    // Validates, writes to disk, and applies the settings.
    // Throws ArgumentException for invalid settings and IOException or
    // UnauthorizedAccessException when the file cannot be written.
    void Save(AppSettings settings);
}
