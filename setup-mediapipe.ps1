param(
    [string]$Version = "0.16.3",
    [string]$OutputPath = "Packages/com.github.homuler.mediapipe-0.16.3.tgz",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$projectRoot = $PSScriptRoot
$targetFile = Join-Path $projectRoot $OutputPath
$targetDir = Split-Path -Parent $targetFile

if ((Test-Path $targetFile) -and -not $Force) {
    Write-Host "MediaPipe package already exists: $targetFile"
    Write-Host "Use -Force to re-download."
    exit 0
}

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$url = "https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v$Version/com.github.homuler.mediapipe-$Version.tgz"

Write-Host "Downloading MediaPipe package..."
Write-Host "Source: $url"
Write-Host "Target: $targetFile"

Invoke-WebRequest -Uri $url -OutFile $targetFile -UseBasicParsing

Write-Host "Done."
