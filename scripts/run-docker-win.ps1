param(
  [string]$Image = $(if ($env:WEB_IMAGE) { $env:WEB_IMAGE } else { 'anilkerai/vibes-asb-manager-web:latest' }),
  [string]$VolumeName = $(if ($env:VOLUME_NAME) { $env:VOLUME_NAME } else { 'vibes-asb-manager-data' }),
  [string]$ContainerName = $(if ($env:CONTAINER_NAME) { $env:CONTAINER_NAME } else { 'vibes-asb-manager' }),
  [int]$Port = $(if ($env:PORT) { [int]$env:PORT } else { 9000 })
)

$ErrorActionPreference = 'Stop'

function Test-Command {
  param([string]$Name)
  $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

if (-not (Test-Command docker)) {
  Write-Error 'Docker is not installed or not in PATH. Please install Docker Desktop.'
}

# Ensure the volume exists
try {
  docker volume inspect $VolumeName *> $null
} catch {
  docker volume create $VolumeName *> $null | Out-Null
}

# Remove existing container if present (ignore errors)
try { docker rm -f $ContainerName *> $null } catch { }

# Run container (pull always to avoid stale cache)
$runArgs = @(
  '--pull','always','-d',
  '--name',$ContainerName,
  '-p',"$Port:8080",
  '-v',"$VolumeName:/app/App_Data",
  '-e','ASPNETCORE_ENVIRONMENT=Development',
  $Image
)

$containerId = docker run @runArgs

$Url = "http://localhost:$Port"
Write-Host "Container '$ContainerName' is running as $containerId."
Write-Host "Open your browser at: $Url"
