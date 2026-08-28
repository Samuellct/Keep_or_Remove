# Builds the plugin and deploys it to the test Jellyfin container.
# Run from the project root: .\docker\scripts\deploy-plugin.ps1

param(
    [string]$Configuration = "Release",
    [string]$PluginName = "Jellyfin.Plugin.KeepOrRemove"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$pluginOutputDir = Join-Path $scriptDir "..\plugins\$PluginName"

Write-Host "Building $PluginName ($Configuration)..."
dotnet build "$projectRoot\src\$PluginName\$PluginName.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

Write-Host "Deploying to test container..."
New-Item -ItemType Directory -Force $pluginOutputDir | Out-Null
Copy-Item "$projectRoot\src\$PluginName\bin\$Configuration\net9.0\Jellyfin.Plugin.KeepOrRemove.dll" $pluginOutputDir -Force

Write-Host "Restarting keeporremove-test container..."
docker compose -f "$scriptDir\..\docker-compose.yml" restart jellyfin

Write-Host "Done. Jellyfin restarting at http://localhost:8098"
