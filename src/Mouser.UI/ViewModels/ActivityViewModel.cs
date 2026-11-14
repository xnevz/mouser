using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Services;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public partial class ActivityViewModel : ViewModelBase
{
    private readonly ActivityLog _log;
    private readonly MouserHost _host;
    private IClipboardAccess? _clipboard;

    public ObservableCollection<ActivityEvent> Entries { get; } = new();
    [ObservableProperty] private string? _statusMessage;

    public ActivityViewModel(ActivityLog log, MouserHost host, IClipboardAccess? clipboard = null)
    {
        _log = log;
        _host = host;
        _clipboard = clipboard;
        Refresh();
        _log.EntryAdded += entry =>
        {
            void Add()
            {
                Entries.Insert(0, entry);
                if (Entries.Count > ActivityLog.Capacity) Entries.RemoveAt(Entries.Count - 1);
            }

            try
            {
                if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    Add();
                else
                    Avalonia.Threading.Dispatcher.UIThread.Post(Add);
            }
            catch
            {
                Add();
            }
        };
    }

    public void AttachClipboard(IClipboardAccess clipboard) => _clipboard = clipboard;

    public void Refresh()
    {
        Entries.Clear();
        foreach (var e in _log.Snapshot())
            Entries.Add(e);
    }

    [RelayCommand]
    public async Task CopyDiagnosticsAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mouser Diagnostics");
        sb.AppendLine($"- OS: {Environment.OSVersion}");
        sb.AppendLine($"- Device Name: {_host.DeviceName}");
        sb.AppendLine($"- Local Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Active Peers: {_host.Peers.Count}");
        sb.AppendLine();
        sb.AppendLine("## Recent Activity Log (last 50)");
        foreach (var entry in _log.Snapshot().Take(50))
        {
            sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Category}] {entry.Message}");
        }

        string text = sb.ToString();
        if (_clipboard != null)
        {
            await _clipboard.SetTextAsync(text);
            StatusMessage = "Diagnostics copied to clipboard.";
        }
    }
}
