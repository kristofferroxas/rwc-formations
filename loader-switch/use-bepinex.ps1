# Switch Mechabellum back to BepInEx (the normal setup), keeping MelonLoader.
# Renames version.dll -> version.dll.off (MelonLoader off, untouched) and
# winhttp.dll.off -> winhttp.dll (BepInEx and its mods on). Nothing is deleted.
param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Mechabellum')
$ErrorActionPreference = 'Stop'
function Fail($t) { Write-Host "Stopped: $t" -ForegroundColor Red; exit 1 }

if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Mechabellum.exe'))) { Fail "no Mechabellum.exe in $GameDir" }
if (Get-Process -Name 'Mechabellum' -ErrorAction SilentlyContinue) { Fail 'Mechabellum is running: close it first.' }
$winhttp = Join-Path $GameDir 'winhttp.dll'
$version = Join-Path $GameDir 'version.dll'

if (Test-Path -LiteralPath $version) { Rename-Item -LiteralPath $version -NewName 'version.dll.off' }
if (-not (Test-Path -LiteralPath $winhttp)) {
    if (Test-Path -LiteralPath "$winhttp.off") { Rename-Item -LiteralPath "$winhttp.off" -NewName 'winhttp.dll' }
    else { Fail 'winhttp.dll.off is missing: BepInEx cannot be switched back on (reinstall BepInEx build 788).' }
}
Write-Host 'Now on BepInEx (MelonLoader is off, kept as version.dll.off).' -ForegroundColor Green
