param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$assets = Join-Path $root "Assets"
$dist = Join-Path $root "dist"
$iconPath = Join-Path $assets "Eslatmachi.ico"
$exePath = Join-Path $dist "Eslatmachi.exe"
New-Item -ItemType Directory -Force -Path $assets, $dist | Out-Null

Add-Type -AssemblyName System.Drawing

function New-RoundedRectangle([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconImage([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)

    $backgroundPath = New-RoundedRectangle 8 8 240 240 56
    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.RectangleF]::new(8, 8, 240, 240),
        [System.Drawing.Color]::FromArgb(22, 146, 133),
        [System.Drawing.Color]::FromArgb(10, 79, 91),
        45.0)
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $shadowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shadowPath.AddEllipse(75, 181, 106, 17)
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35, 0, 38, 40))
    $graphics.FillPath($shadowBrush, $shadowPath)

    $bell = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bell.StartFigure()
    $bell.AddLine(64, 164, 72, 149)
    $bell.AddBezier(72, 149, 69, 135, 70, 121, 75, 106)
    $bell.AddBezier(75, 106, 84, 80, 103, 66, 128, 66)
    $bell.AddBezier(128, 66, 153, 66, 172, 80, 181, 106)
    $bell.AddBezier(181, 106, 186, 121, 187, 135, 184, 149)
    $bell.AddLine(184, 149, 192, 164)
    $bell.AddBezier(192, 164, 196, 171, 190, 176, 181, 176)
    $bell.AddLine(181, 176, 75, 176)
    $bell.AddBezier(75, 176, 66, 176, 60, 171, 64, 164)
    $bell.CloseFigure()
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(249, 253, 251))
    $graphics.FillPath($whiteBrush, $bell)
    $graphics.FillEllipse($whiteBrush, 118, 52, 20, 22)

    $goldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(250, 194, 80))
    $graphics.FillEllipse($goldBrush, 112, 170, 32, 28)
    $graphics.FillEllipse($goldBrush, 181, 48, 40, 40)

    $clockPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(15, 105, 100), 9)
    $clockPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $clockPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($clockPen, 128, 126, 128, 96)
    $graphics.DrawLine($clockPen, 128, 126, 151, 139)
    $centerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(15, 105, 100))
    $graphics.FillEllipse($centerBrush, 122, 120, 12, 12)

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()

    $clockPen.Dispose()
    $centerBrush.Dispose()
    $goldBrush.Dispose()
    $whiteBrush.Dispose()
    $bell.Dispose()
    $shadowBrush.Dispose()
    $shadowPath.Dispose()
    $backgroundBrush.Dispose()
    $backgroundPath.Dispose()
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    return $bytes
}

if (-not (Test-Path $iconPath)) {
    $images = @()
    foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
        $images += , (New-IconImage $size)
    }

    $file = [System.IO.File]::Create($iconPath)
    $writer = New-Object System.IO.BinaryWriter($file)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    for ($index = 0; $index -lt $images.Count; $index++) {
        $size = @(16, 24, 32, 48, 64, 128, 256)[$index]
        $encodedSize = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($image in $images) {
        $file.Write([byte[]]$image, 0, $image.Length)
    }
    $writer.Dispose()
    $file.Dispose()
}

$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path (Join-Path $framework "csc.exe"))) {
    $framework = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}
$compiler = Join-Path $framework "csc.exe"
if (-not (Test-Path $compiler)) {
    throw ".NET Framework C# kompilyatori topilmadi."
}

$sources = Get-ChildItem (Join-Path $root "src") -Filter "*.cs" | ForEach-Object { $_.FullName }
$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/langversion:5",
    "/codepage:65001",
    "/out:$exePath",
    "/win32icon:$iconPath",
    "/win32manifest:$(Join-Path $root 'app.manifest')",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Xml.dll"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Ilovani yig‘ishda xatolik yuz berdi."
}

Write-Host "Tayyor: $exePath"

if ($Install) {
    $installDirectory = Join-Path $env:LOCALAPPDATA "Programs\Eslatmachi"
    $installedExe = Join-Path $installDirectory "Eslatmachi.exe"
    New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
    Copy-Item $exePath $installedExe -Force

    $desktop = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktop "Eslatmachi.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Description = "Ishlaringiz uchun o‘zbekcha eslatmalar"
    $shortcut.Save()
    Write-Host "Ish stoli yorlig‘i: $shortcutPath"
    Write-Host "O‘rnatilgan ilova: $installedExe"
}
