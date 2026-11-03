$sourceImg = 'C:\Users\user\.gemini\antigravity\brain\c9d974fe-87e8-4a8d-bbd9-5ff401e7ca08\razor_formatter_logo_v2_1793725285915.jpg'
Add-Type -AssemblyName System.Drawing

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

$imagesDir = Join-Path $repoRoot "images"
$vscodeImagesDir = Join-Path $repoRoot "src\RazorFormatter.VSCode\images"
$vsDir = Join-Path $repoRoot "src\RazorFormatter.VisualStudio"

New-Item -ItemType Directory -Path $imagesDir -Force | Out-Null
New-Item -ItemType Directory -Path $vscodeImagesDir -Force | Out-Null

$img = [System.Drawing.Image]::FromFile($sourceImg)

# Save standard PNGs
$img.Save((Join-Path $imagesDir "logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$img.Save((Join-Path $imagesDir "icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$img.Save((Join-Path $vscodeImagesDir "icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$img.Save((Join-Path $vsDir "icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)

# Save 128x128 icon (exact Marketplace dimension)
$resized128 = New-Object System.Drawing.Bitmap(128, 128)
$g128 = [System.Drawing.Graphics]::FromImage($resized128)
$g128.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g128.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g128.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g128.DrawImage($img, 0, 0, 128, 128)
$resized128.Save((Join-Path $imagesDir "icon-128.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$resized128.Save((Join-Path $vscodeImagesDir "icon-128.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$resized128.Save((Join-Path $vsDir "icon-128.png"), [System.Drawing.Imaging.ImageFormat]::Png)

$g128.Dispose()
$resized128.Dispose()
$img.Dispose()

Write-Host "Icons successfully generated!"
Get-ChildItem -Path $imagesDir

