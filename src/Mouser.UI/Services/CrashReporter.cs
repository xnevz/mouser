using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using Mouser.Core.Services;

namespace Mouser.UI.Services;

/// <summary>
/// Catches unhandled exceptions from AppDomain, TaskScheduler, and Avalonia Dispatcher.
/// Writes sanitized diagnostics (exception type, message, stack trace) to local log files and ActivityLog.
/// Shows a toast notification. Never uploads anything automatically.
/// </summary>
public sealed class CrashReporter
{
    private static readonly object SyncLock = new();
    private static bool _handlersInstalled;

    public static CrashReporter? Instance { get; set; }

    public static Func<string?>? CurrentClipboardProvider { get; set; }
    public static Action<string, string>? ToastAction { get; set; } = (title, body) => ToastService.Show(title, body);

    public FileLogger Logger { get; }
    public ActivityLog? ActivityLog { get; set; }

    public CrashReporter(FileLogger logger, ActivityLog? activityLog = null)
    {
        Logger = logger;
        ActivityLog = activityLog;
    }

    public static void Install(FileLogger? logger = null, ActivityLog? activityLog = null)
    {
        lock (SyncLock)
        {
            var targetLogger = logger ?? FileLogger.Default;
            if (Instance == null)
            {
                Instance = new CrashReporter(targetLogger, activityLog);
            }
            else
            {
                if (activityLog != null) Instance.ActivityLog = activityLog;
            }

            if (_handlersInstalled) return;
            _handlersInstalled = true;

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Instance?.Handle(ex, "AppDomain", args.IsTerminating);
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Instance?.Handle(args.Exception, "TaskScheduler", isTerminating: false);
                args.SetObserved();
            };

            try
            {
                Dispatcher.UIThread.UnhandledException += (sender, args) =>
                {
                    Instance?.Handle(args.Exception, "Dispatcher", isTerminating: false);
                    args.Handled = true;
                };
            }
            catch
            {
                // Dispatcher might not be initialized yet in early startup or headless environments
            }
        }
    }

    public void Handle(Exception ex, string source, bool isTerminating = false)
    {
        try
        {
            string? clipboardText = null;
            try
            {
                clipboardText = CurrentClipboardProvider?.Invoke();
                if (string.IsNullOrEmpty(clipboardText) && OperatingSystem.IsWindows())
                {
                    clipboardText = Win32Clipboard.GetSnapshot().Text;
                }
            }
            catch
            {
                // Failing to read clipboard during a crash must not prevent logging
            }

            string typeName = ex.GetType().FullName ?? ex.GetType().Name;
            string sanitizedMessage = Sanitize(ex.Message, clipboardText);
            string sanitizedStack = Sanitize(ex.StackTrace ?? string.Empty, clipboardText);

            var sb = new StringBuilder();
            sb.AppendLine($"Unhandled exception ({source}) [terminating={isTerminating}]:");
            sb.AppendLine($"Type: {typeName}");
            sb.AppendLine($"Message: {sanitizedMessage}");
            if (!string.IsNullOrWhiteSpace(sanitizedStack))
            {
                sb.AppendLine($"StackTrace:\n{sanitizedStack}");
            }

            if (ex is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    sb.AppendLine($"Inner: {inner.GetType().FullName}: {Sanitize(inner.Message, clipboardText)}");
                    if (!string.IsNullOrWhiteSpace(inner.StackTrace))
                    {
                        sb.AppendLine($"Inner StackTrace:\n{Sanitize(inner.StackTrace, clipboardText)}");
                    }
                }
            }
            else if (ex.InnerException != null)
            {
                sb.AppendLine($"Inner: {ex.InnerException.GetType().FullName}: {Sanitize(ex.InnerException.Message, clipboardText)}");
                if (!string.IsNullOrWhiteSpace(ex.InnerException.StackTrace))
                {
                    sb.AppendLine($"Inner StackTrace:\n{Sanitize(ex.InnerException.StackTrace, clipboardText)}");
                }
            }

            string logEntry = sb.ToString().TrimEnd();

            // 1. Write to local file log
            Logger.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [Error] {logEntry}");

            // 2. Write to in-memory ActivityLog
            ActivityLog?.Record(ActivityCategory.Error, $"Unhandled exception ({source}): {typeName}: {sanitizedMessage}");

            // 3. Show user notification toast
            try
            {
                ToastAction?.Invoke("Mouser", "Mouser hit a problem. Details are in the activity log.");
            }
            catch
            {
                // Toast failure must not bubble up
            }
        }
        catch
        {
            // Catastrophic crash handling failure must never rethrow
        }
    }

    private static readonly Regex SecretAssignmentRegex = new(
        @"(?i)\b(password|secret|passphrase|token|apikey|api_key|masterkey|master_key|privatekey|recovery_key|key)\b\s*[:=]\s*([^\s,;""']+)",
        RegexOptions.Compiled);

    private static readonly Regex PinAssignmentRegex = new(
        @"(?i)\b(pin|pairing\s*pin)\b\s*[:=]\s*(\d{4,8})",
        RegexOptions.Compiled);

    private static readonly Regex TokenPatternRegex = new(
        @"\b(?:(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,255}|github_pat_[A-Za-z0-9_]{82}|(?:AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}|sk-[A-Za-z0-9_-]{32,}|(?:sk_live|rk_live)_[0-9a-zA-Z]{24,}|eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}|xox[baprs]-[A-Za-z0-9-]{10,}|AIza[0-9A-Za-z_-]{35}|AccountKey=[A-Za-z0-9+/=]{60,})\b",
        RegexOptions.Compiled);

    private static readonly Regex PrivateKeyBlockRegex = new(
        @"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----",
        RegexOptions.Compiled);

    public static string Sanitize(string? text, string? clipboardText = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;

        // 1. Redact clipboard content if present
        if (!string.IsNullOrEmpty(clipboardText) && clipboardText.Length >= 2)
        {
            text = text.Replace(clipboardText, "[REDACTED_CLIPBOARD]", StringComparison.Ordinal);
            string normalizedClip = clipboardText.Replace("\r\n", "\n");
            if (normalizedClip != clipboardText)
            {
                text = text.Replace(normalizedClip, "[REDACTED_CLIPBOARD]", StringComparison.Ordinal);
            }
        }

        // 2. Redact private keys
        text = PrivateKeyBlockRegex.Replace(text, "[REDACTED_PRIVATE_KEY]");

        // 3. Redact common API keys and tokens
        text = TokenPatternRegex.Replace(text, "[REDACTED_SECRET]");

        // 4. Redact key-value assignments
        text = SecretAssignmentRegex.Replace(text, "$1=[REDACTED]");
        text = PinAssignmentRegex.Replace(text, "$1=[REDACTED]");

        return text;
    }
}
