$ErrorActionPreference = "Stop"

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
$script = Join-Path $PSScriptRoot "ChashApp.iss"

if (-not (Test-Path $iscc)) {
    throw "Inno Setup 6 was not found. Install it first: https://jrsoftware.org/isinfo.php"
}

& $iscc $script
Write-Host "Installer created in artifacts\installer"
