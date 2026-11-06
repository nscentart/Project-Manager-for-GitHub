param([string]$Preview = "")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$exe = Join-Path $root ("bin\create-icon-" + [guid]::NewGuid().ToString("N") + ".exe")
New-Item -ItemType Directory -Force -Path (Join-Path $root "bin") | Out-Null
try {
    & $csc -nologo -target:exe -codepage:65001 "-out:$exe" -r:System.Drawing.dll (Join-Path $PSScriptRoot "CreateIcon.cs") (Join-Path $root "src\LogoMark.cs")
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    & $exe (Join-Path $root "assets\ProjectManager.ico")
    if ($LASTEXITCODE -ne 0) { throw "icon not created" }
    "created: assets\ProjectManager.ico"
    if ($Preview -ne "") { & $exe --previews $Preview; "previews in: $Preview" }
} finally { Remove-Item -Force $exe -ErrorAction SilentlyContinue }
