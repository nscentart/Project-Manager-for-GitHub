# crea-icona.ps1 (Project Manager 0.4, 02/11/2026): rifà icona\ProjectManager.ico dal disegno di src\Segno.cs, la stessa
# che la finestra disegna a runtime. Si lancia solo se il disegno cambia; poi si rifà compila.ps1.
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File icona\crea-icona.ps1 [-Foto cartella]
param([string]$Foto = "")
$ErrorActionPreference = 'Stop'
$qui = Split-Path -Parent $MyInvocation.MyCommand.Path
$radice = Split-Path -Parent $qui
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$exe = Join-Path $env:TEMP ("crea-icona-" + [guid]::NewGuid().ToString("N") + ".exe")
try {
    & $csc -nologo -target:exe -codepage:65001 "-out:$exe" -r:System.Drawing.dll (Join-Path $qui "crea-icona.cs") (Join-Path $radice "src\Segno.cs")
    if ($LASTEXITCODE -ne 0) { throw "compilazione fallita" }
    & $exe (Join-Path $qui "ProjectManager.ico")
    if ($LASTEXITCODE -ne 0) { throw "icona non creata" }
    "creata: icona\ProjectManager.ico"
    if ($Foto -ne "") { & $exe --foto $Foto; "foto in: $Foto" }
} finally { Remove-Item -Force $exe -ErrorAction SilentlyContinue }
