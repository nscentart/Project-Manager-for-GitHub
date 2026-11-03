# compila.ps1 (Project Manager 0.1, 21/10/2026): compila src\*.cs in un solo bin\ProjectManager.exe col
# csc del .NET Framework 4 (C# 5, gia' dentro Windows), come WeightMonitor. Nessuna libreria di terzi.
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File compila.ps1 [-Uscita bin]
param([string]$Uscita = "bin")
$ErrorActionPreference = 'Stop'
$qui = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $qui
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path $Uscita | Out-Null
$icona = @()
if (Test-Path "icona\ProjectManager.ico") { $icona = @("-win32icon:icona\ProjectManager.ico") }
& $csc -nologo -target:winexe -optimize -platform:anycpu -codepage:65001 "-out:$Uscita\ProjectManager.exe" `
    "-win32manifest:src\app.manifest" $icona -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll `
    "-resource:lingue\it.txt,lingue.it.txt" "-resource:lingue\en.txt,lingue.en.txt" "-resource:lingue\fr.txt,lingue.fr.txt" "-resource:lingue\es.txt,lingue.es.txt" "-resource:lingue\de.txt,lingue.de.txt" `
    "-recurse:src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "compilazione fallita" }
"compilato: $Uscita\ProjectManager.exe"
