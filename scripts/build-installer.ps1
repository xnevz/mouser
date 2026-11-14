# Builds the single-file portable Windows release and the Inno Setup installer.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 [-Version 0.9.0] [-Sign]

param(
    [string]$Version = "0.9.0",
    [switch]$Sign
)

$ErrorActionPreference = "Stop"

if (Test-Path "C:\Users\abdelkarim\.dotnet") {
    $env:PATH = "C:\Users\abdelkarim\.dotnet;$env:PATH"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$distDir = Join-Path $repoRoot "bin\dist\windows"
$issScript = Join-Path $repoRoot "installer\windows\mouser.iss"

Write-Host "=== Building Mouser Windows Distribution (v$Version) ==="
$publishArgs = @(
    "publish",
    (Join-Path $repoRoot "src\Mouser.Desktop\Mouser.Desktop.csproj"),
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:Version=$Version",
    "-o", $distDir
)

Write-Host "Running: dotnet $($publishArgs -join ' ')"
$dotnetPath = if (Get-Command dotnet -ErrorAction SilentlyContinue) { "dotnet" } else { "C:\Users\abdelkarim\.dotnet\dotnet.exe" }
& $dotnetPath $publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $distDir "Mouser.exe"
if (-not (Test-Path $exePath)) {
    throw "Publish output Mouser.exe not found at $exePath."
}
Write-Host "✓ Portable binary created: $exePath"

# Optional signing step for Mouser.exe
if ($Sign) {
    $pfx = $env:MOUSER_SIGN_PFX
    $password = $env:MOUSER_SIGN_PASSWORD
    if ($pfx -and (Test-Path $pfx)) {
        Write-Host "Signing Mouser.exe with certificate: $pfx"
        & signtool sign /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /f $pfx /p $password $exePath
    } else {
        Write-Host "Skipping binary signing: MOUSER_SIGN_PFX not set or file not found."
    }
}

# Find Inno Setup compiler ISCC.exe
$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if ($candidate -and (Test-Path $candidate)) {
        $isccPath = $candidate
        break
    }
}

if (-not $isccPath) {
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $isccPath = $cmd.Source
    }
}

if (-not $isccPath) {
    Write-Warning "Inno Setup 6 compiler (ISCC.exe) not found."
    Write-Warning "To build the installer, install Inno Setup 6 from https://jrsoftware.org/isdl.php or run:"
    Write-Warning "  winget install JRSoftware.InnoSetup"
    Write-Host "`n✓ Portable build succeeded at $distDir."
    return
}

Write-Host "`n=== Compiling Inno Setup Installer with $isccPath ==="
$isccArgs = @(
    "/DAppVersion=$Version",
    $issScript
)

& $isccPath $isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $repoRoot "bin\dist\Mouser-Setup-$Version.exe"
Write-Host "`n✓ Installer created successfully at: $installerPath"

if ($Sign) {
    $pfx = $env:MOUSER_SIGN_PFX
    $password = $env:MOUSER_SIGN_PASSWORD
    if ($pfx -and (Test-Path $pfx) -and (Test-Path $installerPath)) {
        Write-Host "Signing installer: $installerPath"
        & signtool sign /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /f $pfx /p $password $installerPath
    }
}
