using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyClock.Models;

namespace MyClock.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        Current = Load();
    }

    // %LOCALAPPDATA%\MyClock\settings.json
    public static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyClock",
        "settings.json");

    public AppSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    public void Save(AppSettings settings)
    {
        if (settings.GetValidationError() is { } error)
        {
            throw new ArgumentException(error, nameof(settings));
        }

        var normalized = settings.Normalized();
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write a temporary file first so a failed write never leaves a half-written file.
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(temporaryPath, _settingsPath, overwrite: true);

        Current = normalized;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), JsonOptions);
            return settings?.Normalized() ?? new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A missing or damaged file falls back to defaults. The file is left
            // as it is until the user saves settings again.
            Debug.WriteLine($"Failed to read settings: {exception.Message}");
            return new AppSettings();
        }
    }
}
