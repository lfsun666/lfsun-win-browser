param(
    [string]$Version = "",
    [string]$Notes = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [int]$KeepVersions = 10,
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Lfsun.Win.Browser\Lfsun.Win.Browser.csproj"
$issPath = Join-Path $repoRoot "packaging\LfsunWin.iss"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"
$solutionPath = Join-Path $repoRoot "LfsunWin.sln"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "release"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         LfsunBrowser 自动化构建与发布流水线              " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 确保 dotnet 编译器可用
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $dotnetCandidates = @(
        "E:\DevTools\dotnet",
        $env:DOTNET_ROOT,
        "C:\Program Files\dotnet"
    )
    foreach ($cand in $dotnetCandidates) {
        if ($cand -and (Test-Path (Join-Path $cand "dotnet.exe"))) {
            $env:PATH = "$cand;$env:PATH"
            Write-Host "已自动添加 .NET SDK 到 PATH: $cand" -ForegroundColor DarkGray
            break
        }
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 未找到，请确保已安装 .NET 8 SDK 并加入 PATH。"
}

# 2. 读取并解析当前版本号
$csprojContent = Get-Content $projectPath -Raw
if ($csprojContent -match '<Version>(\d+\.\d+\.\d+)<\/Version>') {
    $currentVersion = $Matches[1]
} else {
    $currentVersion = "0.3.0"
}

Write-Host "当前项目版本: v$currentVersion" -ForegroundColor Yellow

$targetVersion = $currentVersion
if ([string]::IsNullOrWhiteSpace($Version) -or $Version -eq "current") {
    $targetVersion = $currentVersion
} elseif ($Version -eq "patch") {
    $parts = $currentVersion.Split('.')
    $targetVersion = "$($parts[0]).$($parts[1]).$([int]$parts[2] + 1)"
} elseif ($Version -eq "minor") {
    $parts = $currentVersion.Split('.')
    $targetVersion = "$($parts[0]).$([int]$parts[1] + 1).0"
} elseif ($Version -eq "major") {
    $parts = $currentVersion.Split('.')
    $targetVersion = "$([int]$parts[0] + 1).0.0"
} elseif ($Version -match '^\d+\.\d+\.\d+$') {
    $targetVersion = $Version
} else {
    throw "无效的版本号参数: '$Version'。支持: patch, minor, major, current, 或具体版本号如 0.3.1"
}

Write-Host "发布目标版本: v$targetVersion" -ForegroundColor Green

# 3. 更新版本号文件（csproj 和 iss）
if ($targetVersion -ne $currentVersion) {
    Write-Host "更新项目版本号为 v$targetVersion..." -ForegroundColor Cyan
    $newCsproj = $csprojContent -replace '<Version>\d+\.\d+\.\d+<\/Version>', "<Version>$targetVersion</Version>"
    $newCsproj = $newCsproj -replace '<FileVersion>\d+\.\d+\.\d+<\/FileVersion>', "<FileVersion>$targetVersion</FileVersion>"
    Set-Content -Path $projectPath -Value $newCsproj -NoNewline -Encoding UTF8

    $issContent = Get-Content $issPath -Raw
    $newIss = $issContent -replace '#define MyAppVersion "\d+\.\d+\.\d+"', "#define MyAppVersion ""$targetVersion"""
    Set-Content -Path $issPath -Value $newIss -NoNewline -Encoding UTF8
}

# 4. 记录本次更新说明到 CHANGELOG.md
$today = (Get-Date).ToString("yyyy-MM-dd")
if (-not (Test-Path $changelogPath)) {
    Set-Content -Path $changelogPath -Value "# 更新日志 (Changelog)`r`n`r`n" -Encoding UTF8
}

if (-not [string]::IsNullOrWhiteSpace($Notes)) {
    Write-Host "记录更新日志到 CHANGELOG.md..." -ForegroundColor Cyan
    $changelogContent = Get-Content $changelogPath -Raw
    $headerPattern = "## \[$targetVersion\]"
    if ($changelogContent -notmatch $headerPattern) {
        $entryLines = @(
            "## [$targetVersion] - $today",
            "",
            "### 变更内容",
            "- $Notes",
            ""
        )
        $entryText = ($entryLines -join "`r`n") + "`r`n"
        if ($changelogContent -match '(?m)^## ') {
            $firstIndex = $changelogContent.IndexOf("## ")
            $newChangelog = $changelogContent.Substring(0, $firstIndex) + $entryText + $changelogContent.Substring($firstIndex)
        } else {
            $newChangelog = $changelogContent + "`r`n" + $entryText
        }
        Set-Content -Path $changelogPath -Value $newChangelog -NoNewline -Encoding UTF8
    }
}

# 5. 执行单元测试门禁
if (-not $SkipTests) {
    Write-Host "`n[1/4] 执行质量门禁单元测试..." -ForegroundColor Cyan
    dotnet test $solutionPath -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "单元测试未通过，构建发布终止！"
    }
    Write-Host "单元测试全部通过！" -ForegroundColor Green
} else {
    Write-Host "`n[1/4] 跳过单元测试。" -ForegroundColor DarkGray
}

# 6. 确保发布输出目录
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# 7. 编译发布单文件程序
Write-Host "`n[2/4] 编译发布应用程序 (win-x64)..." -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败！"
}

# 8. 打包绿色便携版 (.zip) 到 release/
Write-Host "`n[3/4] 封装绿色免安装便携包..." -ForegroundColor Cyan
$portableZipName = "LfsunBrowser-portable-$Runtime-v$targetVersion.zip"
$portableZipPath = Join-Path $OutputDir $portableZipName
if (Test-Path $portableZipPath) {
    Remove-Item $portableZipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZipPath -Force

# 同步 latest 副本
$portableLatestPath = Join-Path $OutputDir "LfsunBrowser-portable-latest.zip"
Copy-Item $portableZipPath $portableLatestPath -Force
Write-Host "便携包已输出: $portableZipPath" -ForegroundColor Green

# 9. 编译 Inno Setup 安装包 (.exe) 到 release/
if (-not $SkipInstaller) {
    Write-Host "`n[4/4] 编译 Inno Setup 安装包..." -ForegroundColor Cyan

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
        $isccPath = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if ($isccPath) {
        & $isccPath `
            "/DMyAppVersion=$targetVersion" `
            "/DMySourceDir=$publishDir" `
            "/DMyOutputDir=$OutputDir" `
            $issPath

        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup 编译失败！"
        }

        $installerExeName = "LfsunBrowser-setup-$targetVersion.exe"
        $installerExePath = Join-Path $OutputDir $installerExeName
        if (Test-Path $installerExePath) {
            $installerLatestPath = Join-Path $OutputDir "LfsunBrowser-setup-latest.exe"
            Copy-Item $installerExePath $installerLatestPath -Force
            Write-Host "安装包已输出: $installerExePath" -ForegroundColor Green
        }
    } else {
        Write-Warning "未找到 Inno Setup 编译器 (ISCC.exe)，跳过生成安装包。可前往 https://jrsoftware.org/isinfo.php 安装。"
    }
}

# 10. 历史版本保留策略：保留最近 $KeepVersions 个版本，清理超出上限的更旧构建
Write-Host "`n[保留检查] 检查 release 目录历史版本 (保留最近 $KeepVersions 个版本)..." -ForegroundColor Cyan
$versionPattern = '(\d+\.\d+\.\d+)'
$existingFiles = Get-ChildItem -Path $OutputDir -File
$versionMap = @{}

foreach ($f in $existingFiles) {
    if ($f.Name -match "latest" -or $f.Name -eq "SHA256SUMS.txt") {
        continue
    }
    if ($f.Name -match $versionPattern) {
        $vStr = $Matches[1]
        try {
            $parsedV = [System.Version]$vStr
            if (-not $versionMap.ContainsKey($vStr)) {
                $versionMap[$vStr] = @{
                    VersionObj = $parsedV
                    Files = @()
                }
            }
            $versionMap[$vStr].Files += $f
        } catch {}
    }
}

$sortedVersions = $versionMap.Keys | Sort-Object { $versionMap[$_].VersionObj } -Descending

if ($sortedVersions.Count -gt $KeepVersions) {
    $versionsToPrune = $sortedVersions | Select-Object -Skip $KeepVersions
    Write-Host "检测到历史版本总数 ($($sortedVersions.Count)) 超过保留上限 ($KeepVersions)，准备清理旧版本: $($versionsToPrune -join ', ')..." -ForegroundColor Yellow
    foreach ($oldVer in $versionsToPrune) {
        Write-Host "正在清理历史版本 v$oldVer 产物..." -ForegroundColor Yellow
        foreach ($fileToRemove in $versionMap[$oldVer].Files) {
            Write-Host "  删除: $($fileToRemove.Name)" -ForegroundColor DarkGray
            Remove-Item $fileToRemove.FullName -Force -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Host "历史版本数量 ($($sortedVersions.Count)) 在保留上限 ($KeepVersions) 之内，全部保留: $($sortedVersions -join ', ')" -ForegroundColor Green
}

# 11. 生成本次发布的独立说明文件
$releaseNotesFile = Join-Path $OutputDir "RELEASE_NOTES_v$targetVersion.md"
$fileListLines = (Get-ChildItem -Path $OutputDir -File | ForEach-Object { "- **$($_.Name)** ($([math]::Round($_.Length / 1MB, 2)) MB)" }) -join "`r`n"
$notesText = if ($Notes) { $Notes } else { "详见项目 CHANGELOG.md" }
$releaseNotesLines = @(
    "# LfsunBrowser v$targetVersion 发布说明",
    "",
    "- **发布日期**: $today",
    "- **运行时环境**: .NET 8.0 (Self-Contained)",
    "- **目标平台**: Windows x64 (Windows 10/11)",
    "- **版本保留**: 自动保留最近 $KeepVersions 个历史发布版本",
    "",
    "## 交付文件清单",
    $fileListLines,
    "",
    "## 更新摘要",
    $notesText
)
Set-Content -Path $releaseNotesFile -Value ($releaseNotesLines -join "`r`n") -Encoding UTF8

# 12. 重新计算 release 目录全量有效文件的 SHA256 校验和清单
Write-Host "`n生成 release 目录全量文件 SHA256 校验清单..." -ForegroundColor Cyan
$checksumFile = Join-Path $OutputDir "SHA256SUMS.txt"
$checksumLines = @()
$releaseFiles = Get-ChildItem -Path $OutputDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" }
foreach ($file in $releaseFiles) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $checksumLines += "$hash  $($file.Name)"
}
Set-Content -Path $checksumFile -Value ($checksumLines -join "`r`n") -Encoding UTF8

# 13. 最终总结输出
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "            LfsunBrowser v$targetVersion 发布成功!        " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "输出目录: $OutputDir (已保留最近 $([math]::Min($sortedVersions.Count, $KeepVersions)) 个版本产物)" -ForegroundColor White
Get-ChildItem -Path $OutputDir -File | ForEach-Object {
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host ("  {0,-48} {1,8} MB" -f $_.Name, $sizeMb) -ForegroundColor Gray
}
Write-Host "==========================================================" -ForegroundColor Green
