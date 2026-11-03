<#
.SYNOPSIS
    Publishes the RazorFormatter CLI as a .NET Global Tool and standalone self-contained binaries.

.DESCRIPTION
    Builds the RazorFormatter.Cli project as:
    1. A .NET Global Tool NuGet package (nupkg)
    2. Self-contained single-file executables for target runtime identifiers (win-x64, linux-x64, osx-x64, osx-arm64)

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER OutputBase
    Base directory where artifacts will be saved. Default is ../artifacts relative to this script.

.PARAMETER Runtimes
    Array of runtime identifiers (RIDs) to publish standalone executables for.

.PARAMETER PackTool
    Switch indicating whether to create the .NET Global Tool package. Default is $true.

.PARAMETER PublishStandalone
    Switch indicating whether to publish self-contained single-file binaries. Default is $true.

.EXAMPLE
    ./scripts/publish-cli.ps1
    ./scripts/publish-cli.ps1 -Runtimes @("win-x64")
    ./scripts/publish-cli.ps1 -PackTool:$true -PublishStandalone:$false
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputBase = "",
    [string[]]$Runtimes = @("win-x64", "linux-x64", "osx-x64", "osx-arm64"),
    [bool]$PackTool = $true,
    [bool]$PublishStandalone = $true
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputBase)) {
    $OutputBase = Join-Path $PSScriptRoot "..\artifacts"
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "src\RazorFormatter.Cli\RazorFormatter.Cli.csproj"
$outputDir = [System.IO.Path]::GetFullPath($OutputBase)

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " RazorFormatter CLI Distribution Publisher" -ForegroundColor Cyan
Write-Host " Configuration: $Configuration" -ForegroundColor Gray
Write-Host " Output Directory: $outputDir" -ForegroundColor Gray
Write-Host "====================================================" -ForegroundColor Cyan

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 1. Package .NET Global Tool
if ($PackTool) {
    $toolOutputDir = Join-Path $outputDir "tool"
    Write-Host "`n[1/2] Packing .NET Global Tool..." -ForegroundColor Yellow
    
    dotnet pack $projectPath `
        --configuration $Configuration `
        --output $toolOutputDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pack .NET Global Tool."
    }

    Write-Host "Global Tool package generated at: $toolOutputDir" -ForegroundColor Green
}

# 2. Publish Self-Contained Single-File Executables
if ($PublishStandalone) {
    Write-Host "`n[2/2] Publishing Self-Contained Single-File Executables..." -ForegroundColor Yellow

    foreach ($rid in $Runtimes) {
        $targetDir = Join-Path $outputDir $rid
        Write-Host "`n -> Publishing for RID: $rid..." -ForegroundColor Magenta

        dotnet publish $projectPath `
            --configuration $Configuration `
            --runtime $rid `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            --output $targetDir

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish for runtime: $rid"
        }

        Write-Host " -> Successfully published $rid to: $targetDir" -ForegroundColor Green
    }
}

Write-Host "`n====================================================" -ForegroundColor Cyan
Write-Host " CLI Distribution Build Complete!" -ForegroundColor Green
Write-Host " Artifacts stored at: $outputDir" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan
