param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Lfsun.Win.Browser\Lfsun.Win.Browser.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerOutDir = if (-not [string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir } else { Join-Path $repoRoot "artifacts\installer" }
$issPath = Join-Path $repoRoot "packaging\LfsunWin.iss"

# 自动解析版本号
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojContent = Get-Content $projectPath -Raw
    if ($csprojContent -match '<Version>(\d+\.\d+\.\d+)<\/Version>') {
        $Version = $Matches[1]
    } else {
        $Version = "0.3.0"
    }
}

# 确保 dotnet 可用
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $dotnetCandidates = @("E:\DevTools\dotnet", $env:DOTNET_ROOT, "C:\Program Files\dotnet")
    foreach ($cand in $dotnetCandidates) {
        if ($cand -and (Test-Path (Join-Path $cand "dotnet.exe"))) {
            $env:PATH = "$cand;$env:PATH"
            break
        }
    }
}

Write-Host "Publishing installer payload for v$Version..."
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
if (-not (Test-Path $installerOutDir)) {
    New-Item -ItemType Directory -Path $installerOutDir -Force | Out-Null
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
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "D:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "D:\Program Files\Inno Setup 6\ISCC.exe",
        "E:\Program Files\Inno Setup 6\ISCC.exe"
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
