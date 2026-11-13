using System;
using System.IO;
using System.Text.Json;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.UI.Services;

public sealed class AppSettings
{
    public string DeviceId { get; set; } = Guid.NewGuid().ToString("N");
    public int PairingPin { get; set; } = PairingGate.GeneratePin();
    public int InputPort { get; set; } = InputInjectionService.DefaultPort;
    public bool AcceptRemoteInput { get; set; } = true;
    public bool ClipboardHistoryEnabled { get; set; } = true;
    public bool SkipSensitiveClips { get; set; } = true;
    public int ClipboardRetentionDays { get; set; } = 30;
    public bool StartAtLogin { get; set; }
}

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in the per-user data folder.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mouser");

    public string FilePath { get; }
    public string DataDirectory => Path.GetDirectoryName(FilePath)!;
    public AppSettings Current { get; }

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(DefaultDataDirectory, "settings.json");
        Directory.CreateDirectory(DataDirectory);
        Current = Load();
        Save();
    }

    public void Save()
    {
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
        File.Move(tmp, FilePath, overwrite: true);
    }

    private AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new AppSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            if (settings.PairingPin is < 0 or > 999_999) settings.PairingPin = PairingGate.GeneratePin();
            if (settings.InputPort is < 1 or > 65535) settings.InputPort = InputInjectionService.DefaultPort;
            if (settings.ClipboardRetentionDays < 1) settings.ClipboardRetentionDays = 30;
            return settings;
        }
        catch (JsonException)
        {
            // Keep the unreadable file for inspection instead of silently overwriting it.
            File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
            return new AppSettings();
        }
    }
}
