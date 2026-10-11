# Publishes a new version into ./release and restarts the NotifyGymOccupancy scheduled task.
# Usage, from anywhere: .\release.ps1

$ErrorActionPreference = "Stop"

$taskName = "NotifyGymOccupancy"
$releaseDir = Join-Path $PSScriptRoot "release"
$exePath = Join-Path $releaseDir "NotifyGymOccupancy.exe"

$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($task) {
    Write-Host "Stopping $taskName..."
    Stop-ScheduledTask -TaskName $taskName
}

# Wait for the app to exit so its files are unlocked, and force it if it lingers
$deadline = (Get-Date).AddSeconds(10)
while ($running = Get-Process -Name "NotifyGymOccupancy" -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }) {
    if ((Get-Date) -gt $deadline) {
        $running | Stop-Process -Force
        break
    }
    Start-Sleep -Milliseconds 500
}

Write-Host "Publishing to $releaseDir..."
dotnet publish (Join-Path $PSScriptRoot "src") -c Release -o $releaseDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed, the task has been left stopped." -ForegroundColor Red
    exit $LASTEXITCODE
}

if ($task) {
    Start-ScheduledTask -TaskName $taskName
    Write-Host "Released and restarted $taskName." -ForegroundColor Green
}
else {
    Write-Host "Published, but no '$taskName' task is registered yet. See the README to create it." -ForegroundColor Yellow
}
