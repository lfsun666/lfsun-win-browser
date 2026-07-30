param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Lfsun.Win.Browser\Lfsun.Win.Browser.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerOutDir = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $repoRoot "packaging\LfsunWin.iss"

Write-Host "Publishing installer payload..."
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
if (-not (Test-Path $installerOutDir)) {
    New-Item -ItemType Directory -Path $installerOutDir | Out-Null
}

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$isccCmd = Get-Command iscc -ErrorAction SilentlyContinue
$isccPath = if ($isccCmd) { $isccCmd.Source } else { $null }
if (-not $isccPath) {
    $candidatePaths = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $isccPath = $candidatePaths |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
}

if (-not $isccPath) {
    throw "Inno Setup compiler 'ISCC.exe' not found. Please install Inno Setup first: https://jrsoftware.org/isinfo.php"
}

Write-Host "Building installer with Inno Setup..."
& $isccPath `
    "/DMyAppVersion=$Version" `
    "/DMySourceDir=$publishDir" `
    "/DMyOutputDir=$installerOutDir" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "iscc failed."
}

Write-Host "Installer output folder: $installerOutDir"

