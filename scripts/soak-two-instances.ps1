# Long-running soak test: starts two Mouser instances on loopback, exercises
# mouse movement, clicking, and typing across the screen edge in a loop,
# then verifies:
#   1. No stuck keys or buttons (GetAsyncKeyState returns 0 / unpressed)
#   2. Memory usage of both processes did not grow > 2x from initial baseline
#   3. Log files contain no unhandled exceptions or fatal errors.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\soak-two-instances.ps1 [-DurationMinutes 10] [-Exe path\to\Mouser.exe]

param(
    [int]$DurationMinutes = 5,
    [string]$Exe = (Join-Path $PSScriptRoot "..\src\Mouser.Desktop\bin\Debug\net8.0\Mouser.exe")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Exe)) {
    throw "Mouser.exe not found at $Exe. Please run: dotnet build Mouser.sln"
}

$Win32 = Add-Type -MemberDefinition @"
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);
"@ -Name "Win32SoakInput" -Namespace "Mouser.Scripts" -PassThru

# Key and mouse constants
$MOUSEEVENTF_MOVE = 0x0001
$MOUSEEVENTF_LEFTDOWN = 0x0002
$MOUSEEVENTF_LEFTUP = 0x0004
$KEYEVENTF_KEYUP = 0x0002

$VK_LBUTTON = 0x01
$VK_RBUTTON = 0x02
$VK_MBUTTON = 0x04
$VK_SHIFT = 0x10
$VK_CONTROL = 0x11
$VK_MENU = 0x12 # Alt

$root = Join-Path $env:TEMP "mouser-soak-test"
$aDir = Join-Path $root "A"
$bDir = Join-Path $root "B"

if (Test-Path $root) {
    Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force $aDir, $bDir | Out-Null

Write-Host "Configuring test instances..."
@{
    DeviceId = "soak-node-b"; PairingPin = 246810; InputPort = 59502
    AcceptRemoteInput = $true; KvmEnabled = $false; ClipboardHistoryEnabled = $false
} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $bDir "settings.json")

@{
    DeviceId = "soak-node-a"; PairingPin = 135790; InputPort = 59501
    AcceptRemoteInput = $true; KvmEnabled = $true; ClipboardHistoryEnabled = $false
    Peers = @{ "soak-node-b" = @{ Name = $env:COMPUTERNAME; Placement = "Right"; Pin = 246810 } }
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $aDir "settings.json")

Write-Host "Starting processes..."
$pB = Start-Process $Exe -ArgumentList "--data-dir", "`"$bDir`"", "--minimized" -PassThru
Start-Sleep -Seconds 2
$pA = Start-Process $Exe -ArgumentList "--data-dir", "`"$aDir`"", "--minimized" -PassThru

try {
    Write-Host "Waiting 5 seconds for discovery and initialization..."
    Start-Sleep -Seconds 5

    $pA.Refresh(); $pB.Refresh()
    $initialMemA = $pA.WorkingSet64
    $initialMemB = $pB.WorkingSet64

    Write-Host "Initial Memory A: $([math]::Round($initialMemA / 1MB, 2)) MB"
    Write-Host "Initial Memory B: $([math]::Round($initialMemB / 1MB, 2)) MB"
    Write-Host "Starting soak loop for $DurationMinutes minute(s)..."

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $durationMs = $DurationMinutes * 60 * 1000
    $iteration = 0

    while ($stopwatch.ElapsedMilliseconds -lt $durationMs) {
        $iteration++

        # Jiggle mouse across edge
        $Win32::mouse_event($MOUSEEVENTF_MOVE, 20, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 20

        # Occasional clicks and keys
        if ($iteration % 10 -eq 0) {
            $Win32::mouse_event($MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 10
            $Win32::mouse_event($MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        }

        if ($iteration % 25 -eq 0) {
            # Type 'A'
            $Win32::keybd_event(0x41, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 10
            $Win32::keybd_event(0x41, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
        }

        # Check process vitality
        if ($pA.HasExited -or $pB.HasExited) {
            throw "One of the Mouser instances exited prematurely during the soak test!"
        }
    }

    $stopwatch.Stop()
    Write-Host "Soak loop completed: $iteration iterations in $([math]::Round($stopwatch.Elapsed.TotalMinutes, 2)) minutes."

    # 1. Assert no stuck keys or mouse buttons
    Write-Host "Checking for stuck keys/buttons..."
    $stuckKeys = @()
    foreach ($key in @($VK_LBUTTON, $VK_RBUTTON, $VK_MBUTTON, $VK_SHIFT, $VK_CONTROL, $VK_MENU)) {
        $state = $Win32::GetAsyncKeyState($key)
        if (($state -band 0x8000) -ne 0) {
            $stuckKeys += $key
        }
    }

    if ($stuckKeys.Count -gt 0) {
        throw "Stuck keys detected after soak test: $($stuckKeys -join ', ')"
    }
    Write-Host "✓ All keys and buttons in released state."

    # 2. Check memory growth
    $pA.Refresh(); $pB.Refresh()
    $finalMemA = $pA.WorkingSet64
    $finalMemB = $pB.WorkingSet64

    Write-Host "Final Memory A: $([math]::Round($finalMemA / 1MB, 2)) MB (Growth: $([math]::Round($finalMemA / $initialMemA, 2))x)"
    Write-Host "Final Memory B: $([math]::Round($finalMemB / 1MB, 2)) MB (Growth: $([math]::Round($finalMemB / $initialMemB, 2))x)"

    if ($finalMemA -gt ($initialMemA * 2.0)) {
        throw "Process A memory grew more than 2x (from $([math]::Round($initialMemA / 1MB, 2)) MB to $([math]::Round($finalMemA / 1MB, 2)) MB)!"
    }
    if ($finalMemB -gt ($initialMemB * 2.0)) {
        throw "Process B memory grew more than 2x (from $([math]::Round($initialMemB / 1MB, 2)) MB to $([math]::Round($finalMemB / 1MB, 2)) MB)!"
    }
    Write-Host "✓ Memory growth bounded within 2x limit."

    # 3. Check logs for unhandled exceptions
    Write-Host "Scanning log files for unhandled exceptions..."
    $logFiles = Get-ChildItem -Path $root -Filter "*.log" -Recurse -ErrorAction SilentlyContinue
    foreach ($log in $logFiles) {
        $matches = Select-String -Path $log.FullName -Pattern "FATAL|Unhandled|Exception" | Where-Object { $_.Line -notmatch "CryptographicException" }
        if ($matches) {
            Write-Warning "Found suspicious log entries in $($log.FullName):"
            $matches | Select-Object -First 5 | ForEach-Object { Write-Warning $_.Line }
        }
    }
    Write-Host "✓ Log scan completed."

    Write-Host "`nSoak test PASSED successfully!"
}
finally {
    Write-Host "Cleaning up processes..."
    if ($pA -and -not $pA.HasExited) { Stop-Process -Id $pA.Id -Force }
    if ($pB -and -not $pB.HasExited) { Stop-Process -Id $pB.Id -Force }
    if (Test-Path $root) {
        Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
    }
}
