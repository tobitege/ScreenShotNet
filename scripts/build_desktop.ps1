<#
Builds ScreenShotNet desktop project (and optionally tests).

Usage:
  pwsh ./scripts/build_desktop.ps1
  pwsh ./scripts/build_desktop.ps1 -Configuration Release
  pwsh ./scripts/build_desktop.ps1 -IncludeTests
  pwsh ./scripts/build_desktop.ps1 -EnableModernTfms
  pwsh ./scripts/build_desktop.ps1 -NoRestore
#>
param(
    [string]$Configuration = "Debug",
    [switch]$IncludeTests,
    [switch]$EnableModernTfms,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$script:buildResults = @()
$script:startTime = Get-Date

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Command
    )

    Write-Host $Title -ForegroundColor Cyan
    Write-Host "  $Command"
    $stepStart = Get-Date
    Invoke-Expression $Command
    $stepDuration = (Get-Date) - $stepStart

    if ($LASTEXITCODE -ne 0) {
        $script:buildResults += [PSCustomObject]@{ Project = $Title; Status = "FAILED"; Duration = $stepDuration }
        Write-Host "FAILED: $Title" -ForegroundColor Red
        exit 1
    }
    $script:buildResults += [PSCustomObject]@{ Project = $Title; Status = "OK"; Duration = $stepDuration }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$desktopProject = Join-Path $repoRoot "src/ScreenShotNet.csproj"
$testsProject = Join-Path $repoRoot "tests/ScreenShotNet.Tests/ScreenShotNet.Tests.csproj"

if (!(Test-Path $desktopProject)) {
    throw "Desktop project not found: $desktopProject"
}

$noRestoreArg = if ($NoRestore) { " --no-restore" } else { "" }
$modernTfmsArg = if ($EnableModernTfms) { " -p:EnableModernTfms=true" } else { "" }

Invoke-Step "Build: ScreenShotNet" "dotnet build `"$desktopProject`" -c $Configuration$modernTfmsArg$noRestoreArg"

if ($IncludeTests) {
    if (!(Test-Path $testsProject)) {
        throw "Test project not found: $testsProject"
    }
    Invoke-Step "Build: ScreenShotNet.Tests" "dotnet build `"$testsProject`" -c $Configuration$modernTfmsArg$noRestoreArg"
}

$totalDuration = (Get-Date) - $script:startTime

Write-Host ""
Write-Host "----------------------------------------------------------" -ForegroundColor Green
Write-Host " BUILD SUMMARY" -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor Green
Write-Host ""
foreach ($result in $script:buildResults) {
    $statusColor = if ($result.Status -eq "OK") { "Green" } else { "Red" }
    $duration = $result.Duration.ToString("mm\:ss\.ff")
    Write-Host ("  [{0}] {1,-40} {2}" -f $result.Status, $result.Project, $duration) -ForegroundColor $statusColor
}
Write-Host ""
Write-Host ("  Total time: {0:mm\:ss\.ff}" -f $totalDuration) -ForegroundColor Cyan
Write-Host ("  Projects built: {0}" -f $script:buildResults.Count) -ForegroundColor Cyan
Write-Host ""
Write-Host "All builds completed successfully." -ForegroundColor Green