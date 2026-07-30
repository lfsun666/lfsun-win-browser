param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Lfsun.Win.Browser\Lfsun.Win.Browser.csproj"
$portableDir = Join-Path $repoRoot "artifacts\portable\$Runtime"
$zipPath = Join-Path $repoRoot "artifacts\portable\LfsunBrowser-portable-$Runtime.zip"

Write-Host "Publishing portable build..."
if (Test-Path $portableDir) {
    Remove-Item $portableDir -Recurse -Force
}

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $portableDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating portable zip..."
Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zipPath -Force

Write-Host "Portable output:"
Write-Host "  Folder: $portableDir"
Write-Host "  Zip:    $zipPath"


