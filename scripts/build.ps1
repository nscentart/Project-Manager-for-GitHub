param([string]$Output = "bin")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$icon = @()
if (Test-Path "assets\ProjectManager.ico") { $icon = @("-win32icon:assets\ProjectManager.ico") }
& $csc -nologo -target:winexe -optimize -platform:anycpu -codepage:65001 "-out:$Output\ProjectManager.exe" `
    "-win32manifest:assets\app.manifest" $icon -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll `
    "-resource:locales\it.txt,locales.it.txt" "-resource:locales\en.txt,locales.en.txt" "-resource:locales\fr.txt,locales.fr.txt" "-resource:locales\es.txt,locales.es.txt" "-resource:locales\de.txt,locales.de.txt" `
    "-recurse:src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
"built: $Output\ProjectManager.exe"
