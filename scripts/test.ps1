param([string]$Exe = "")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($Exe -eq "") { $Exe = Join-Path $root "bin\ProjectManager.exe" }
$scratchRoot = Join-Path $root "bin\test-scratch"
$screenerFolder = Join-Path $root "tests\screener"

function Invoke-Suite($name, $arguments) {
    $resultFile = Join-Path $script:scratch "$name.txt"
    $process = Start-Process -FilePath $Exe -ArgumentList ($arguments + "`"$resultFile`"") -Wait -PassThru
    Write-Host "--- $name"
    if (Test-Path $resultFile) { Get-Content -Path $resultFile -Encoding UTF8 } else { Write-Host "FAIL: no result" }
    if ($process.ExitCode -ne 0) { Write-Host "RED: suite $name has at least one failing check (see FAIL above)"; exit 1 }
}

function New-Scratch {
    $folder = Join-Path $scratchRoot ([guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $env:PM_DATA_DIR = Join-Path $folder "data"
    return $folder
}

$script:scratch = New-Scratch
try {
    Invoke-Suite "locales" @("--test-locales", "`"$root`"")
    Invoke-Suite "live-locales" @("--test-live-locales")
    Invoke-Suite "screener" @("--test-screener", "`"$screenerFolder`"")
    foreach ($language in "en", "fr", "es", "de") {
        Invoke-Suite "language-command-$language" @("--language", $language, "--test-language-command")
        Invoke-Suite "screener-$language" @("--language", $language, "--test-screener", "`"$screenerFolder`"")
    }
    Invoke-Suite "organizations" @("--test-organizations")
    Invoke-Suite "overview" @("--test-overview")
    Invoke-Suite "accounts" @("--test-accounts")
    Invoke-Suite "wizard" @("--test-wizard")
    Invoke-Suite "writes" @("--test-writes")
    Invoke-Suite "install" @("--test-install")
    Invoke-Suite "stats" @("--test-stats")
    Invoke-Suite "missing-repos" @("--test-missing-repos")
    Invoke-Suite "update-notice" @("--test-update-notice")
    Invoke-Suite "retry" @("--test-retry")
    Invoke-Suite "migration" @("--test-migration")
    Invoke-Suite "repo" @("--test-repo", "`"$root`"")
    Write-Host "GREEN"
} finally { Remove-Item -Recurse -Force $script:scratch -ErrorAction SilentlyContinue }

$script:scratch = New-Scratch
try {
    Invoke-Suite "license" @("--test-license")
    Write-Host "GREEN (license)"
} finally { Remove-Item -Recurse -Force $script:scratch -ErrorAction SilentlyContinue }
