using System;

namespace Mouser.Core.Kvm;

/// <summary>
/// Pure policy logic for cross-screen drag and drop file transfer (Task E17).
/// Evaluates whether an edge crossing with the mouse button held should trigger
/// an automated file transfer between peers.
/// </summary>
public static class DragDropPolicy
{
    public const long DefaultMaxDragSizeBytes = 500L * 1024 * 1024; // 500 MB

    /// <summary>
    /// Determines whether cross-screen drag-and-drop should initiate.
    /// </summary>
    public static bool ShouldStartDrag(
        bool isLeftButtonDown,
        bool isSourceExplorer,
        long totalSizeBytes,
        long maxSizeBytes = DefaultMaxDragSizeBytes)
    {
        if (!isLeftButtonDown) return false;
        if (!isSourceExplorer) return false;
        if (totalSizeBytes <= 0) return false;
        if (totalSizeBytes > maxSizeBytes) return false;

        return true;
    }

    /// <summary>
    /// Checks if a window belongs to Windows Explorer / Shell file manager.
    /// </summary>
    public static bool IsExplorerWindow(string? processName, string? windowClassName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        bool isExplorerExe = processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                             processName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);

        if (!isExplorerExe) return false;

        // Common Explorer shell window classes:
        // CabinetWClass (Explorer window), ExploreWClass, WorkerW / Progman (Desktop)
        if (string.IsNullOrWhiteSpace(windowClassName)) return true;

        return windowClassName.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
               windowClassName.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
               windowClassName.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
               windowClassName.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Evaluates if file sizes exceed the allowed drag budget.
    /// </summary>
    public static bool ExceedsSizeBudget(long totalSizeBytes, long maxSizeBytes = DefaultMaxDragSizeBytes)
    {
        return totalSizeBytes > maxSizeBytes;
    }
}
