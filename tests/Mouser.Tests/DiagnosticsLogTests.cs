using System;
using System.IO;
using System.Linq;
using Mouser.Core.Clipboard;
using Mouser.Core.Services;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class DiagnosticsLogTests
{
    [Fact]
    public void Rotation_Keeps7NewestFiles_When10DailyFilesExist()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mouser-test-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new FileLogger(tempDir);
            var baseDate = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // Create 10 daily files
            for (int i = 0; i < 10; i++)
            {
                var date = baseDate.AddDays(i);
                string filePath = Path.Combine(tempDir, $"mouser-{date:yyyyMMdd}.log");
                File.WriteAllText(filePath, $"Log entry for {date:yyyy-MM-dd}");
                File.SetLastWriteTimeUtc(filePath, date);
            }

            Assert.Equal(10, Directory.GetFiles(tempDir, "mouser-*.log").Length);

            // Trigger rotation / cleanup
            logger.CleanOldLogs();

            var remainingFiles = Directory.GetFiles(tempDir, "mouser-*.log");
            Assert.Equal(7, remainingFiles.Length);

            // Verify the 3 oldest files are deleted (days 0, 1, 2)
            for (int i = 0; i < 3; i++)
            {
                var oldDate = baseDate.AddDays(i);
                string oldPath = Path.Combine(tempDir, $"mouser-{oldDate:yyyyMMdd}.log");
                Assert.False(File.Exists(oldPath), $"File {oldPath} should have been rotated out.");
            }

            // Verify the 7 newest files are kept (days 3 through 9)
            for (int i = 3; i < 10; i++)
            {
                var newDate = baseDate.AddDays(i);
                string newPath = Path.Combine(tempDir, $"mouser-{newDate:yyyyMMdd}.log");
                Assert.True(File.Exists(newPath), $"File {newPath} should be kept.");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void ExceptionLine_NeverContainsCurrentClipboardContent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mouser-test-clip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new FileLogger(tempDir);
            var reporter = new CrashReporter(logger);

            const string sentinel = "SENTINEL_SECRET_CLIPBOARD_TEXT_987654";
            CrashReporter.CurrentClipboardProvider = () => sentinel;

            // Forced crash where the exception message attempts to include the sentinel clipboard text
            var ex = new InvalidOperationException($"Crash while reading clipboard content: {sentinel}");
            reporter.Handle(ex, "TestForcedCrash");

            string logFile = logger.CurrentLogFilePath;
            Assert.True(File.Exists(logFile));
            string content = File.ReadAllText(logFile);

            // Verify the sentinel is completely absent
            Assert.DoesNotContain(sentinel, content);
            Assert.Contains("[REDACTED_CLIPBOARD]", content);
            Assert.Contains("System.InvalidOperationException", content);

            // Second test: forced crash with an unrelated exception while sentinel is on clipboard
            var ex2 = new ArgumentException("Unrelated parameter error");
            reporter.Handle(ex2, "TestForcedCrash2");

            content = File.ReadAllText(logFile);
            Assert.DoesNotContain(sentinel, content);

            // If running on Windows, test with real Win32Clipboard snapshot fallback
            if (OperatingSystem.IsWindows())
            {
                CrashReporter.CurrentClipboardProvider = null;
                Win32Clipboard.SetSnapshot(new ClipboardSnapshot(sentinel, null, null, null));

                var ex3 = new Exception($"Win32 crash: {sentinel}");
                reporter.Handle(ex3, "Win32ForcedCrash");

                content = File.ReadAllText(logFile);
                Assert.DoesNotContain(sentinel, content);
            }
        }
        finally
        {
            CrashReporter.CurrentClipboardProvider = null;
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void ErrorHandling_LogsType_Message_AndStackTrace()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mouser-test-err-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new FileLogger(tempDir);
            var activityLog = new ActivityLog(logger);
            var reporter = new CrashReporter(logger, activityLog);

            string? shownToastTitle = null;
            string? shownToastBody = null;
            CrashReporter.ToastAction = (t, b) =>
            {
                shownToastTitle = t;
                shownToastBody = b;
            };

            Exception thrownException;
            try
            {
                throw new InvalidOperationException("Critical database disk write failure");
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            reporter.Handle(thrownException, "ErrorHandlingTest");

            string logFile = logger.CurrentLogFilePath;
            Assert.True(File.Exists(logFile));
            string content = File.ReadAllText(logFile);

            // Assert type, message, and stack trace are logged
            Assert.Contains("System.InvalidOperationException", content);
            Assert.Contains("Critical database disk write failure", content);
            Assert.Contains("at Mouser.Tests.DiagnosticsLogTests", content); // Stack trace

            // Assert ActivityLog received the event
            var snapshot = activityLog.Snapshot();
            Assert.NotEmpty(snapshot);
            var errorEvent = snapshot.First(e => e.Category == ActivityCategory.Error);
            Assert.Contains("System.InvalidOperationException", errorEvent.Message);
            Assert.Contains("Critical database disk write failure", errorEvent.Message);

            // Assert toast was triggered with exact text
            Assert.Equal("Mouser", shownToastTitle);
            Assert.Equal("Mouser hit a problem. Details are in the activity log.", shownToastBody);
        }
        finally
        {
            CrashReporter.ToastAction = (title, body) => ToastService.Show(title, body);
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void CrashReporter_Handle_IsReentrancySafe_DoesNotStackOverflow()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mouser_crash_reentrant_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new FileLogger(tempDir);
            var reporter = new CrashReporter(logger);
            int callCount = 0;
            CrashReporter.ToastAction = (t, b) =>
            {
                callCount++;
                if (callCount < 50)
                {
                    reporter.Handle(new InvalidOperationException("Nested exception inside toast"), "Nested");
                }
            };

            reporter.Handle(new InvalidOperationException("Initial exception"), "Initial");

            Assert.Equal(1, callCount);
        }
        finally
        {
            CrashReporter.ToastAction = (title, body) => ToastService.Show(title, body);
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void FileLogger_RollsOver_WhenFileExceedsMaxSizeBytes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mouser-test-roll-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new FileLogger(tempDir)
            {
                MaxFileSizeBytes = 500 // Small threshold for rollover test
            };

            // Write entries until rollover occurs
            string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string baseFile = Path.Combine(tempDir, $"mouser-{dateStr}.log");
            string rolledFile = Path.Combine(tempDir, $"mouser-{dateStr}.1.log");

            for (int i = 0; i < 15; i++)
            {
                logger.WriteLine(new string('X', 100));
            }

            Assert.True(File.Exists(baseFile), "Base log file should exist.");
            Assert.True(File.Exists(rolledFile), "Rolled log file (.1.log) should exist after exceeding max size.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void ActivityViewModel_OpenLogFolder_ExecutesActionWithLogDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mouser-test-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = new FileLogger(tempDir);
            var log = new ActivityLog(logger);
            var settings = new SettingsStore(Path.Combine(tempDir, "settings.json"));
            var host = new MouserHost(settings, new RecordingSink());
            var vm = new ActivityViewModel(log, host);

            string? openedPath = null;
            vm.OpenFolderAction = path => openedPath = path;

            vm.OpenLogFolderCommand.Execute(null);

            Assert.NotNull(openedPath);
            Assert.Equal(tempDir, openedPath);
            Assert.True(Directory.Exists(openedPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void CrashReporter_Sanitize_MasksSecretsTokensAndPins()
    {
        string input = "Error: token=ghp_abcdefghijklmnopqrstuvwxyz1234567890 key: sk-live_123456789012345678901234 pin: 123456 password=MySecretPassword!";
        string sanitized = CrashReporter.Sanitize(input);

        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz1234567890", sanitized);
        Assert.DoesNotContain("sk-live_123456789012345678901234", sanitized);
        Assert.DoesNotContain("123456", sanitized);
        Assert.DoesNotContain("MySecretPassword!", sanitized);

        Assert.Contains("[REDACTED", sanitized);
    }
}
