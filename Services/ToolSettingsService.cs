using System.IO;
using System.Text.Json;
using Sts2SaveSyncTool.Models;

namespace Sts2SaveSyncTool.Services;

public sealed class ToolSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public ToolSettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2",
            "save_sync_tool",
            "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public ToolSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new ToolSettings();
        }

        try
        {
            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<ToolSettings>(json, JsonOptions) ?? new ToolSettings();
        }
        catch
        {
            return new ToolSettings();
        }
    }

    public void Save(ToolSettings settings)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("无法解析设置文件目录。");
        }

        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
