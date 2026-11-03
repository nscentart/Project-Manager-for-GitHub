# Scatta le foto delle schermate in una o più lingue, con i soli dati d'esempio del programma.
# Esempio: .\foto.ps1 -Cartella C:\foto -Lingue it,en,fr,es,de
# I file si chiamano <schermata>-<lingua>.png. Non apre finestre sullo schermo e non tocca i dati veri.
param(
    [string]$Cartella = "foto",
    [string[]]$Lingue = @("it", "en", "fr", "es", "de"),
    [string]$Exe = "bin\ProjectManager.exe"
)
$ErrorActionPreference = "Stop"
$qui = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not [IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $qui $Exe }
if (-not (Test-Path $Exe)) { throw "Manca il programma: compila prima con compila.ps1." }
if (-not [IO.Path]::IsPathRooted($Cartella)) { $Cartella = Join-Path (Get-Location).Path $Cartella }
New-Item -ItemType Directory -Force $Cartella | Out-Null
$sbagliate = 0
foreach ($l in ($Lingue -join "," -split ",")) {
    $dati = Join-Path ([IO.Path]::GetTempPath()) ("gpm-foto-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force $dati | Out-Null
    $env:GPM_DATI = $dati
    try {
        $p = Start-Process -FilePath $Exe -ArgumentList @("--foto", ('"' + $Cartella + '"'), $l) -Wait -PassThru -WindowStyle Hidden
        if ($p.ExitCode -ne 0) { $sbagliate++; Write-Output "$l : foto non riuscite (codice $($p.ExitCode))" }
        else { Write-Output "$l : fatto" }
    } finally {
        Remove-Item Env:GPM_DATI
        Remove-Item -Recurse -Force $dati -ErrorAction SilentlyContinue
    }
}
if ($sbagliate -gt 0) { exit 1 }
Write-Output "Foto in $Cartella"
