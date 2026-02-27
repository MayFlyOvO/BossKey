using System.Text.Json;
using HideProcess.Core.Models;

namespace HideProcess.Core.Services;

public sealed class JsonSettingsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; }

    public JsonSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(appData, "HideProcess");
        SettingsPath = Path.Combine(appDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            var defaults = new AppSettings();
            if (!File.Exists(SettingsPath))
            {
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions);
            if (settings is null)
            {
                return defaults;
            }

            return Normalize(settings, defaults);
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public AppSettings ImportFromPath(string path)
    {
        var defaults = new AppSettings();
        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions)
            ?? throw new InvalidDataException("Invalid settings file.");
        return Normalize(settings, defaults);
    }

    public void ExportToPath(AppSettings settings, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        File.WriteAllText(path, json);
    }

    private static AppSettings Normalize(AppSettings settings, AppSettings defaults)
    {
        settings.Targets ??= [];
        settings.HideHotkey = settings.HideHotkey?.IsValid == true ? settings.HideHotkey : defaults.HideHotkey;
        settings.ShowHotkey = settings.ShowHotkey?.IsValid == true ? settings.ShowHotkey : defaults.ShowHotkey;
        settings.Language = string.IsNullOrWhiteSpace(settings.Language) ? defaults.Language : settings.Language;
        return settings;
    }
}
