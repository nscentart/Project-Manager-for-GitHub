# prova.ps1 (Project Manager 1.1, 03/11/2026): tutte le prove, senza rete e senza toccare i dati veri: la dogana sui testi di
# prove\dogana, le cinque lingue, l'organizzazione, la Panoramica, gli account, la procedura guidata e l'installazione in una cartella temporanea.
# Rosso (codice 1) se una sbaglia. Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File prova.ps1 [-Exe altro.exe]
param([string]$Exe = "")
$ErrorActionPreference = 'Stop'
$qui = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Exe -eq "") { $Exe = Join-Path $qui "bin\ProjectManager.exe" }
$temp = Join-Path $env:TEMP ("gpm-prova-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$env:GPM_DATI = Join-Path $temp "dati"   # il log e i dati delle prove stanno qui, mai nella cartella vera
function Prova($nome, $argomenti) {
    $esito = Join-Path $temp "$nome.txt"
    $p = Start-Process -FilePath $Exe -ArgumentList ($argomenti + "`"$esito`"") -Wait -PassThru
    Write-Host "--- $nome"
    if (Test-Path $esito) { Get-Content -Path $esito -Encoding UTF8 } else { Write-Host "SBAGLIA: nessun esito" }
    if ($p.ExitCode -ne 0) { Write-Host "ROSSO: la prova $nome sbaglia almeno un caso (vedi SBAGLIA sopra)"; exit 1 }
}
try {
    Prova "lingue" @("--prova-lingue", "`"$qui`"")
    Prova "lingue-vive" @("--prova-lingue-vive")
    Prova "dogana" @("--prova-dogana", "`"$(Join-Path $qui 'prove\dogana')`"")
    foreach ($l in "en", "fr", "es", "de") {
        # la dogana non deve dipendere dalla lingua dell'interfaccia, e --lingua vale con ogni comando senza scriversi
        Prova "lingua-comando-$l" @("--lingua", $l, "--prova-lingua-comando")
        Prova "dogana-$l" @("--lingua", $l, "--prova-dogana", "`"$(Join-Path $qui 'prove\dogana')`"")
    }
    Prova "organizzazione" @("--prova-organizzazione")
    Prova "panoramica" @("--prova-panoramica")
    Prova "accessi" @("--prova-accessi")
    Prova "guidata" @("--prova-guidata")
    Prova "scrittura" @("--prova-scrittura")
    Prova "installazione" @("--prova-installazione")
    Prova "numeri" @("--prova-numeri")
    Prova "sparite" @("--prova-sparite")
    Prova "avviso" @("--prova-avviso")
    Prova "riprova" @("--prova-riprova")
    Prova "repo" @("--prova-repo", "`"$qui`"")
    Write-Host "VERDE"
} finally { Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue }

# ---------- la licenza: un blocco a parte, con la sua cartella temporanea. Rosso se sbaglia, anche dopo il VERDE qui sopra ----------
$temp = Join-Path $env:TEMP ("gpm-prova-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$env:GPM_DATI = Join-Path $temp "dati"
try {
    Prova "licenza" @("--prova-licenza")
    Write-Host "VERDE (licenza)"
} finally { Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue }
