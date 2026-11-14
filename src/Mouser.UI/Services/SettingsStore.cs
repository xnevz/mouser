using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.UI.Services;

public sealed class AppSettings
{
    public string DeviceId { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceName { get; set; } = Environment.MachineName;
    public int PairingPin { get; set; } = PairingGate.GeneratePin();
    public int InputPort { get; set; } = InputInjectionService.DefaultPort;
    public bool AcceptRemoteInput { get; set; } = true;
    public bool ClipboardHistoryEnabled { get; set; } = true;
    public bool SaveImages { get; set; } = true;
    public bool SkipSensitiveClips { get; set; } = true;
    public int ClipboardRetentionDays { get; set; } = 30;
    public int ClipboardRetentionHours { get; set; } = 720;
    public bool ClearOnExit { get; set; } = false;
    public string PanicWipeHotkey { get; set; } = string.Empty;
    public HotkeySettings Hotkeys { get; set; } = new();
    private string _quickPasteHotkey = "Win+Alt+V";
    public string QuickPasteHotkey
    {
        get => Hotkeys?.QuickPaste ?? _quickPasteHotkey;
        set
        {
            _quickPasteHotkey = value;
            if (Hotkeys != null) Hotkeys.QuickPaste = value;
        }
    }
    public bool StartAtLogin { get; set; }
    public bool RunAsAdministrator { get; set; } = false;
    public string Theme { get; set; } = "System";

    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool PauseSharing { get; set; }
    public bool PauseClipboard { get; set; }
    public bool Notifications { get; set; } = true;
    public bool WarnElevatedWindows { get; set; } = true;
    public bool FirstRunDone { get; set; }

    public int AutoLockMinutes { get; set; } = 0;
    public bool LockWithWindows { get; set; } = true;

    /// <summary>Send copied text to paired computers and accept theirs.</summary>
    public bool ClipboardSyncEnabled { get; set; } = true;

    /// <summary>Share this computer's mouse and keyboard with placed peers.</summary>
    public bool KvmEnabled { get; set; } = true;

    /// <summary>Edge resistance feel when crossing to a peer ("Relaxed", "Normal", "Careful").</summary>
    public string SwitchFeel { get; set; } = "Normal";

    public bool BlockInFullscreen { get; set; } = true;
    public bool BlockInMaximized { get; set; } = false;
    public bool BlockInCorners { get; set; } = false;
    public string RequireModifier { get; set; } = "None";
    public bool DoubleTapOverride { get; set; } = true;
    public bool WrapAround { get; set; } = false;
    public bool ProportionalEntry { get; set; } = true;
    public bool DimScreensOnControl { get; set; } = true;
    public double DimScreensOpacity { get; set; } = 0.4;

    /// <summary>Other Mouser computers, keyed by device id.</summary>
    public Dictionary<string, PeerSettings> Peers { get; set; } = new();

    public List<string> ManualPeers { get; set; } = new();

    /// <summary>Applications whose clipboard operations should not be captured or synced (e.g. password managers).</summary>
    public List<string> IgnoredApps { get; set; } = new()
    {
        "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane", "enpass"
    };
}

public sealed class HotkeySettings
{
    public string ReturnControl { get; set; } = "Ctrl+Alt+Esc";
    public string LockCursor { get; set; } = "";
    public string QuickPaste { get; set; } = "Win+Alt+V";
    public bool PanicDoublePressCtrl { get; set; } = true;
    public string Jump1 { get; set; } = "Ctrl+Alt+1";
    public string Jump2 { get; set; } = "Ctrl+Alt+2";
    public string Jump3 { get; set; } = "Ctrl+Alt+3";
    public string Jump4 { get; set; } = "";
    public string Jump5 { get; set; } = "";
    public string Jump6 { get; set; } = "";
    public string Jump7 { get; set; } = "";
    public string Jump8 { get; set; } = "";
    public string Jump9 { get; set; } = "";
    public string NextComputer { get; set; } = "Ctrl+Alt+Right";
}

public sealed class PeerSettings
{
    public string Name { get; set; } = string.Empty;
    public PeerPlacement Placement { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    /// <summary>The peer's PIN, kept so pairing survives restarts (like the phone app does).</summary>
    public int? Pin { get; set; }
    public bool SendClipboard { get; set; } = true;
    public bool ReceiveClipboard { get; set; } = true;
    public bool SendFiles { get; set; } = true;
    public double MouseSpeed { get; set; } = 1.0;
    public double ScrollSpeed { get; set; } = 1.0;
    public bool InvertScroll { get; set; }
    public bool SyncLock { get; set; } = false;
    public bool SyncSleep { get; set; } = false;
    public bool SyncDisplayOff { get; set; } = false;
}

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in the per-user data folder.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

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
            var rawJson = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(rawJson, JsonOptions) ?? new AppSettings();
            settings.Peers ??= new();
            foreach (var peer in settings.Peers.Values)
            {
                peer.MouseSpeed = Math.Clamp(peer.MouseSpeed, 0.25, 4.0);
                peer.ScrollSpeed = Math.Clamp(peer.ScrollSpeed, 0.25, 4.0);
            }
            settings.ManualPeers ??= new();
            settings.IgnoredApps ??= new() { "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane", "enpass" };
            if (string.IsNullOrWhiteSpace(settings.DeviceName)) settings.DeviceName = Environment.MachineName;
            if (settings.PairingPin is < 0 or > 999_999) settings.PairingPin = PairingGate.GeneratePin();
            if (settings.InputPort is < 1 or > 65535) settings.InputPort = InputInjectionService.DefaultPort;
            if (settings.ClipboardRetentionDays < 1) settings.ClipboardRetentionDays = 30;
            if (!rawJson.Contains(nameof(AppSettings.ClipboardRetentionHours)) || settings.ClipboardRetentionHours == 0)
            {
                if (settings.ClipboardRetentionDays > 0)
                {
                    settings.ClipboardRetentionHours = settings.ClipboardRetentionDays * 24;
                }
                else
                {
                    settings.ClipboardRetentionHours = 720;
                }
            }
            settings.Hotkeys ??= new();
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.ReturnControl))
                settings.Hotkeys.ReturnControl = "Ctrl+Alt+Esc";
            if (settings.Hotkeys.LockCursor == null)
                settings.Hotkeys.LockCursor = "";
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.NextComputer))
                settings.Hotkeys.NextComputer = "Ctrl+Alt+Right";
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.Jump1))
                settings.Hotkeys.Jump1 = "Ctrl+Alt+1";
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.Jump2))
                settings.Hotkeys.Jump2 = "Ctrl+Alt+2";
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.Jump3))
                settings.Hotkeys.Jump3 = "Ctrl+Alt+3";
            settings.Hotkeys.Jump4 ??= "";
            settings.Hotkeys.Jump5 ??= "";
            settings.Hotkeys.Jump6 ??= "";
            settings.Hotkeys.Jump7 ??= "";
            settings.Hotkeys.Jump8 ??= "";
            settings.Hotkeys.Jump9 ??= "";

            if (!rawJson.Contains(nameof(AppSettings.Hotkeys)))
            {
                if (!string.IsNullOrWhiteSpace(settings.QuickPasteHotkey))
                    settings.Hotkeys.QuickPaste = settings.QuickPasteHotkey;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(settings.Hotkeys.QuickPaste))
                    settings.Hotkeys.QuickPaste = "Win+Alt+V";
                settings.QuickPasteHotkey = settings.Hotkeys.QuickPaste;
            }

            if (string.IsNullOrWhiteSpace(settings.QuickPasteHotkey)) settings.QuickPasteHotkey = "Win+Alt+V";
            // SaveImages default is true
            if (settings.AutoLockMinutes < 0) settings.AutoLockMinutes = 0;
            if (!ThemeService.Options.Contains(settings.Theme)) settings.Theme = "System";
            if (settings.SwitchFeel != "Relaxed" && settings.SwitchFeel != "Normal" && settings.SwitchFeel != "Careful")
            {
                settings.SwitchFeel = "Normal";
            }
            if (settings.RequireModifier != "None" && settings.RequireModifier != "Ctrl" && settings.RequireModifier != "Alt" && settings.RequireModifier != "Shift")
            {
                settings.RequireModifier = "None";
            }
            if (double.IsNaN(settings.DimScreensOpacity))
            {
                settings.DimScreensOpacity = 0.4;
            }
            else
            {
                settings.DimScreensOpacity = Math.Clamp(settings.DimScreensOpacity, 0.0, 0.7);
            }
            // Pre-existing settings files from before B13 do not nag existing users.
            if (!rawJson.Contains(nameof(AppSettings.FirstRunDone)))
            {
                settings.FirstRunDone = true;
            }
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
