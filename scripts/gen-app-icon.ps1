# 生成 LfsunBrowser 品牌图标 app.ico（多尺寸 PNG-based ICO）
# 几何设计与 MainWindow.CreateDefaultWindowIcon 保持一致：
#   渐变圆角方块底座 + 白色几何 "L" 字标 + 右上轨道圆点
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\Lfsun.Win.Browser\app.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param([float]$X, [float]$Y, [float]$W, [float]$H, [float]$Radius)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-LogoBitmap {
    param([int]$Size)
    $s = $Size / 64.0
    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # 底座：45° 渐变圆角方块（品牌蓝，与浅色模式 Accent 一致的色系）
    $baseRect = New-Object System.Drawing.RectangleF((4 * $s), (4 * $s), (56 * $s), (56 * $s))
    $topColor = [System.Drawing.Color]::FromArgb(255, 86, 156, 233)
    $bottomColor = [System.Drawing.Color]::FromArgb(255, 16, 62, 138)
    $bodyBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($baseRect, $topColor, $bottomColor, 45.0)
    $shape = New-RoundedRectPath (4 * $s) (4 * $s) (56 * $s) (56 * $s) (16 * $s)
    $g.FillPath($bodyBrush, $shape)

    # 字标：几何 "L"（竖笔 + 横笔），白色实心
    $white = [System.Drawing.Brushes]::White
    $stem = New-RoundedRectPath (20 * $s) (15 * $s) (10 * $s) (26 * $s) (5 * $s)
    $foot = New-RoundedRectPath (20 * $s) (31 * $s) (25 * $s) (10 * $s) (5 * $s)
    $g.FillPath($white, $stem)
    $g.FillPath($white, $foot)

    # 轨道圆点：右上角半透明白点
    $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
    $g.FillEllipse($dotBrush, (39 * $s), (15 * $s), (9 * $s), (9 * $s))

    $dotBrush.Dispose(); $foot.Dispose(); $stem.Dispose(); $shape.Dispose(); $bodyBrush.Dispose(); $g.Dispose()
    return $bitmap
}

# 组装 PNG-based ICO：ICONDIR(6) + ICONDIRENTRY(16 x N) + PNG 数据
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBlobs = @()
foreach ($size in $sizes) {
    $bmp = New-LogoBitmap -Size $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBlobs += , $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()
}

$outStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($outStream)
$writer.Write([uint16]0)              # 保留
$writer.Write([uint16]1)              # 类型：ICO
$writer.Write([uint16]$sizes.Count)   # 图像数量

$dataOffset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $blob = $pngBlobs[$i]
    $dimByte = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dimByte)     # 宽
    $writer.Write([byte]$dimByte)     # 高
    $writer.Write([byte]0)            # 调色板色数
    $writer.Write([byte]0)            # 保留
    $writer.Write([uint16]1)          # 颜色平面
    $writer.Write([uint16]32)         # 位深
    $writer.Write([uint32]$blob.Length)
    $writer.Write([uint32]$dataOffset)
    $dataOffset += $blob.Length
}
foreach ($blob in $pngBlobs) {
    $writer.Write($blob)
}
$writer.Flush()

$resolved = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.File]::WriteAllBytes($resolved, $outStream.ToArray())
$writer.Dispose(); $outStream.Dispose()

Write-Host "已生成图标：$resolved（$($sizes -join ', ') px）"
