# Tests desktop-to-desktop control on ONE Windows PC by running two Mouser instances:
#   A = the controlling computer, with B paired and placed to its RIGHT
#   B = the "other computer" (it is the same desktop, so the cursor wraps around)
# Push the cursor past the right edge of your screen: it reappears at the left edge at the same
# height, now driven through A -> network -> B. Clicks and typing go through B too.
# Move back past the left edge (or press Ctrl+Alt+Esc / Scroll Lock) to return to A.
#
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\test-kvm-single-pc.ps1 [-Exe path\to\Mouser.exe]
# Build first:  dotnet build src\Mouser.Desktop
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\src\Mouser.Desktop\bin\Debug\net8.0\Mouser.exe")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Exe)) { throw "Mouser.exe not found at $Exe. Run: dotnet build src\Mouser.Desktop" }

$root = Join-Path $env:TEMP "mouser-kvm-test"
$a = Join-Path $root "A"
$b = Join-Path $root "B"
New-Item -ItemType Directory -Force $a, $b | Out-Null

# Fixed ids, PINs and ports (not the defaults, so a normal Mouser can keep running).
@{
    DeviceId = "kvm-test-b"; PairingPin = 246810; InputPort = 58502
    AcceptRemoteInput = $true; KvmEnabled = $false; ClipboardHistoryEnabled = $false
} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $b "settings.json")

@{
    DeviceId = "kvm-test-a"; PairingPin = 135790; InputPort = 58501
    AcceptRemoteInput = $true; KvmEnabled = $true; ClipboardHistoryEnabled = $false
    Peers = @{ "kvm-test-b" = @{ Name = $env:COMPUTERNAME; Placement = "Right"; Pin = 246810 } }
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $a "settings.json")

$pb = Start-Process $Exe -ArgumentList "--data-dir", "`"$b`"" -PassThru
Start-Sleep -Seconds 2
$pa = Start-Process $Exe -ArgumentList "--data-dir", "`"$a`"" -PassThru

Write-Host ""
Write-Host "Started Mouser (A) pid $($pa.Id) and Mouser (B) pid $($pb.Id)."
Write-Host "Wait ~5 seconds until A's 'Displays & Network' page shows B as Paired, then push the cursor off the RIGHT edge."
Write-Host "Ctrl+Alt+Esc or Scroll Lock always gives control back. Close both windows to finish."
