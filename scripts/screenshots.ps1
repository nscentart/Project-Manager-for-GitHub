param(
    [string]$Folder = "",
    [string[]]$Languages = @("it", "en", "fr", "es", "de"),
    [string]$Exe = "bin\ProjectManager.exe"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not [IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $root $Exe }
if (-not (Test-Path $Exe)) { throw "Program not found: run scripts\build.ps1 first." }
if ($Folder -eq "") { $Folder = Join-Path $root "bin\screenshots" }
if (-not [IO.Path]::IsPathRooted($Folder)) { $Folder = Join-Path (Get-Location).Path $Folder }
New-Item -ItemType Directory -Force $Folder | Out-Null
$failed = 0
foreach ($language in ($Languages -join "," -split ",")) {
    $dataFolder = Join-Path $root ("bin\test-scratch\screenshots-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force $dataFolder | Out-Null
    $env:PM_DATA_DIR = $dataFolder
    try {
        $process = Start-Process -FilePath $Exe -ArgumentList @("--screenshots", ('"' + $Folder + '"'), $language) -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -ne 0) { $failed++; Write-Output "$language : screenshots failed (code $($process.ExitCode))" }
        else { Write-Output "$language : done" }
    } finally {
        Remove-Item Env:PM_DATA_DIR
        Remove-Item -Recurse -Force $dataFolder -ErrorAction SilentlyContinue
    }
}
if ($failed -gt 0) { exit 1 }
Write-Output "Screenshots in $Folder"
