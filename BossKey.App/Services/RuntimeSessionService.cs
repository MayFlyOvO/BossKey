using System.IO;
using System.Text.Json;

namespace BossKey.App.Services;

public sealed class RuntimeSessionService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _sessionPath;

    public RuntimeSessionService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(appData, "BossKey");
        _sessionPath = Path.Combine(appDirectory, "session.json");
    }

    public bool BeginSession()
    {
        var previous = Load();
        var hadUnexpectedExit = previous?.IsRunning == true;

        Save(new RuntimeSessionState
        {
            IsRunning = true,
            LastStartUtc = DateTime.UtcNow,
            LastExitUtc = previous?.LastExitUtc,
            LastExitWasGraceful = false
        });

        return hadUnexpectedExit;
    }

    public void EndSessionGracefully()
    {
        var previous = Load();
        Save(new RuntimeSessionState
        {
            IsRunning = false,
            LastStartUtc = previous?.LastStartUtc ?? DateTime.UtcNow,
            LastExitUtc = DateTime.UtcNow,
            LastExitWasGraceful = true
        });
    }

    private RuntimeSessionState? Load()
    {
        try
        {
            if (!File.Exists(_sessionPath))
            {
                return null;
            }

            var json = File.ReadAllText(_sessionPath);
            return JsonSerializer.Deserialize<RuntimeSessionState>(json, _serializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private void Save(RuntimeSessionState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_sessionPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, _serializerOptions);
            File.WriteAllText(_sessionPath, json);
        }
        catch
        {
        }
    }

    private sealed class RuntimeSessionState
    {
        public bool IsRunning { get; set; }
        public DateTime LastStartUtc { get; set; }
        public DateTime? LastExitUtc { get; set; }
        public bool LastExitWasGraceful { get; set; }
    }
}
