using System.Text.Json;
using BossKey.Core.Models;

namespace BossKey.Core.Services;

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
        var appDirectory = Path.Combine(appData, "BossKey");
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
        settings.Groups ??= [];
        settings.HideHotkey = NormalizeHotkeyBinding(settings.HideHotkey, defaults.HideHotkey);
        settings.ShowHotkey = NormalizeHotkeyBinding(settings.ShowHotkey, defaults.ShowHotkey);
        settings.Language = string.IsNullOrWhiteSpace(settings.Language) ? defaults.Language : settings.Language;
        settings.Theme = ThemeSettings.Normalize(settings.Theme);
        foreach (var target in settings.Targets)
        {
            EnsureTargetDefaults(target);
        }

        foreach (var group in settings.Groups)
        {
            group.Id = string.IsNullOrWhiteSpace(group.Id) ? Guid.NewGuid().ToString("N") : group.Id;
            group.IconColor = TargetGroupConfig.NormalizeIconColor(group.IconColor, group.Id);
            group.Targets ??= [];
            group.HideHotkey = NormalizeHotkeyBinding(group.HideHotkey, new HotkeyBinding());
            group.ShowHotkey = NormalizeHotkeyBinding(group.ShowHotkey, new HotkeyBinding());
            foreach (var target in group.Targets)
            {
                EnsureTargetDefaults(target);
            }
        }

        if (settings.Groups.Count == 0)
        {
            settings.Groups.Add(new TargetGroupConfig
            {
                Id = TargetGroupConfig.DefaultGroupId,
                Name = string.Empty,
                IconColor = TargetGroupConfig.GetDefaultIconColor(TargetGroupConfig.DefaultGroupId),
                Targets = settings.Targets
                    .Select(static target => new TargetAppConfig
                    {
                        Id = target.Id,
                        ProcessName = target.ProcessName,
                        ProcessPath = target.ProcessPath,
                        Enabled = target.Enabled,
                        MuteOnHide = target.MuteOnHide,
                        FreezeOnHide = target.FreezeOnHide,
                        TopMostOnShow = target.TopMostOnShow,
                        CenterOnCursorOnShow = target.CenterOnCursorOnShow
                    })
                    .ToList()
            });
        }

        var defaultGroup = settings.Groups.FirstOrDefault(group =>
            string.Equals(group.Id, TargetGroupConfig.DefaultGroupId, StringComparison.OrdinalIgnoreCase));
        if (defaultGroup is null)
        {
            defaultGroup = new TargetGroupConfig
            {
                Id = TargetGroupConfig.DefaultGroupId,
                Name = string.Empty,
                IconColor = TargetGroupConfig.GetDefaultIconColor(TargetGroupConfig.DefaultGroupId)
            };
            settings.Groups.Insert(0, defaultGroup);
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedGroupHotkeyId) ||
            settings.Groups.All(group => !string.Equals(group.Id, settings.SelectedGroupHotkeyId, StringComparison.Ordinal)))
        {
            settings.SelectedGroupHotkeyId = defaultGroup.Id;
        }

        settings.Targets = settings.Groups
            .SelectMany(static group => group.Targets)
            .Select(static target => new TargetAppConfig
            {
                Id = target.Id,
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide,
                FreezeOnHide = target.FreezeOnHide,
                TopMostOnShow = target.TopMostOnShow,
                CenterOnCursorOnShow = target.CenterOnCursorOnShow
            })
            .ToList();

        return settings;
    }

    private static void EnsureTargetDefaults(TargetAppConfig target)
    {
        target.Id = string.IsNullOrWhiteSpace(target.Id) ? Guid.NewGuid().ToString("N") : target.Id;
    }

    private static HotkeyBinding NormalizeHotkeyBinding(HotkeyBinding? binding, HotkeyBinding fallback)
    {
        if (binding is null)
        {
            return HotkeyBinding.FromKeys(fallback.Keys);
        }

        binding.Keys ??= [];
        return binding;
    }
}

