<#
.SYNOPSIS
    Packages both the VS Code extension and Visual Studio extension into .vsix packages.

.PARAMETER Version
    The semantic version string (e.g., 1.0.0).

.PARAMETER Configuration
    Build configuration (Release or Debug). Default is Release.

.PARAMETER OutputDir
    Output directory for the generated .vsix packages. Default is artifacts/vsix.
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "..\artifacts\vsix"
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputFullPath = [System.IO.Path]::GetFullPath($OutputDir)

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " RazorFormatter Extension Packaging (VSIX)" -ForegroundColor Cyan
Write-Host " Version: $Version" -ForegroundColor Gray
Write-Host " Configuration: $Configuration" -ForegroundColor Gray
Write-Host " Output Directory: $outputFullPath" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Cyan

if (-not (Test-Path $outputFullPath)) {
    New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null
}

# -----------------------------------------------------------------------------
# 1. Package VS Code / Cursor / Antigravity Extension (.vsix)
# -----------------------------------------------------------------------------
Write-Host "`n[1/2] Packaging VS Code Extension..." -ForegroundColor Yellow
$vscodeDir = Join-Path $repoRoot "src\RazorFormatter.VSCode"
$packageJsonContent = Get-Content (Join-Path $vscodeDir "package.json") -Raw | ConvertFrom-Json
$extName = $packageJsonContent.name
$vscodeVsixPath = Join-Path $outputFullPath "$extName-$Version.vsix"

Push-Location $vscodeDir
try {
    # Ensure dependencies and fresh TypeScript compilation
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm dependencies in VSCode extension..."
        npm install
    }
    npm run build

    Write-Host "Executing @vscode/vsce package..."
    npx @vscode/vsce package --no-dependencies -o $vscodeVsixPath
    Write-Host " -> VS Code extension packaged: $vscodeVsixPath" -ForegroundColor Green
}
finally {
    Pop-Location
}

# -----------------------------------------------------------------------------
# 2. Package Visual Studio 2022 Extension (.vsix)
# -----------------------------------------------------------------------------
Write-Host "`n[2/2] Packaging Visual Studio 2022 Extension..." -ForegroundColor Yellow
$vsProj = Join-Path $repoRoot "src\RazorFormatter.VisualStudio\RazorFormatter.VisualStudio.csproj"
$vsVsixPath = Join-Path $outputFullPath "RazorFormatter.VisualStudio-$Version.vsix"
$manifestSource = Join-Path $repoRoot "src\RazorFormatter.VisualStudio\source.extension.vsixmanifest"

dotnet build $vsProj --configuration $Configuration

$vsBinDir = Join-Path $repoRoot "src\RazorFormatter.VisualStudio\bin\$Configuration\net10.0"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

$tempStaging = Join-Path $outputFullPath "vs-staging-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempStaging -Force | Out-Null

try {
    if (Test-Path $vsVsixPath) {
        Remove-Item $vsVsixPath -Force
    }

    $vsInstallPath = if (Test-Path $vswhere) { & $vswhere -latest -products * -property installationPath } else { $null }
    $vsixUtil = if ($vsInstallPath) { Join-Path $vsInstallPath "VSSDK\VisualStudioIntegration\Tools\Bin\VsixUtil.exe" } else { $null }

    if ($vsixUtil -and (Test-Path $vsixUtil)) {
        Write-Host "Packaging Visual Studio VSIX using Microsoft VSSDK VsixUtil.exe..." -ForegroundColor Yellow
        $fileListJson = Join-Path $tempStaging "filelist.json"
        
        @{
            Files = @(
                @{ Path = (Resolve-Path (Join-Path $vsBinDir "RazorFormatter.VisualStudio.dll")).Path },
                @{ Path = (Resolve-Path (Join-Path $vsBinDir "RazorFormatter.Core.dll")).Path }
            )
        } | ConvertTo-Json | Set-Content -LiteralPath $fileListJson -Encoding UTF8

        & $vsixUtil package -outputPath $vsVsixPath -sourceManifest $manifestSource -files $fileListJson -is64BitBuild
        if ($LASTEXITCODE -ne 0) {
            Write-Error "VsixUtil.exe failed to build Visual Studio extension package."
        }
    } else {
        Write-Warning "VSSDK VsixUtil.exe not found; falling back to OPC zip packaging."
        Copy-Item (Join-Path $vsBinDir "RazorFormatter.VisualStudio.dll") $tempStaging
        Copy-Item (Join-Path $vsBinDir "RazorFormatter.Core.dll") $tempStaging
        Copy-Item $manifestSource (Join-Path $tempStaging "extension.vsixmanifest")

        $contentTypes = @"
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="vsixmanifest" ContentType="text/xml" />
  <Default Extension="dll" ContentType="application/octet-stream" />
  <Default Extension="json" ContentType="application/json" />
</Types>
"@
        Set-Content -LiteralPath (Join-Path $tempStaging "[Content_Types].xml") -Value $contentTypes -Encoding UTF8

        $tempZipPath = Join-Path $outputFullPath "vs-package-temp.zip"
        if (Test-Path $tempZipPath) { Remove-Item $tempZipPath -Force }
        Compress-Archive -Path "$tempStaging\*" -DestinationPath $tempZipPath -CompressionLevel Optimal
        Move-Item -Path $tempZipPath -Destination $vsVsixPath -Force
    }

    Write-Host " -> Visual Studio extension packaged: $vsVsixPath" -ForegroundColor Green
}
finally {
    if (Test-Path $tempStaging) {
        Remove-Item -Recurse -Force $tempStaging
    }
}

Write-Host "`n====================================================" -ForegroundColor Cyan
Write-Host " Extension Packaging Complete!" -ForegroundColor Green
Write-Host " VSIX Packages stored at: $outputFullPath" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan
