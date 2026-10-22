# Switch Mechabellum to MelonLoader, keeping BepInEx (for testing the MelonLoader build).
#
# BepInEx starts through winhttp.dll and MelonLoader through version.dll. This only
# renames those two files: winhttp.dll -> winhttp.dll.off (BepInEx and all its mods
# off, untouched) and version.dll.off -> version.dll. The first time, it unpacks
# MelonLoader from -MelonZip after checking the zip's SHA-256. Nothing is deleted.
# Back: use-bepinex.ps1.
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Mechabellum',
    [string]$MelonZip,                                                                 # first time only
    [string]$MelonSha256 = '5b2b2f3d1cd42b59ec886c5bdc2663edae87a0097a4f4a8f58c0965a99dda416'  # v0.7.3 x64, from GitHub
)
$ErrorActionPreference = 'Stop'
function Fail($t) { Write-Host "Stopped: $t" -ForegroundColor Red; exit 1 }

if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Mechabellum.exe'))) { Fail "no Mechabellum.exe in $GameDir" }
if (Get-Process -Name 'Mechabellum' -ErrorAction SilentlyContinue) { Fail 'Mechabellum is running: close it first.' }
$winhttp = Join-Path $GameDir 'winhttp.dll'
$version = Join-Path $GameDir 'version.dll'

if (-not (Test-Path -LiteralPath $version)) {
    if (Test-Path -LiteralPath "$version.off") { Rename-Item -LiteralPath "$version.off" -NewName 'version.dll' }
    else {
        if (-not $MelonZip) { Fail 'MelonLoader is not in the game folder yet: pass -MelonZip <path to MelonLoader.x64.zip>.' }
        if ((Get-FileHash -LiteralPath $MelonZip -Algorithm SHA256).Hash -ne $MelonSha256.ToUpper()) { Fail 'the MelonLoader zip does not match the expected SHA-256.' }
        foreach ($p in 'MelonLoader', 'version.dll') { if (Test-Path -LiteralPath (Join-Path $GameDir $p)) { Fail "$p is already in the game folder; not overwriting it." } }
        Expand-Archive -LiteralPath $MelonZip -DestinationPath $GameDir
        Write-Host 'MelonLoader unpacked into the game folder.'
    }
}
if (Test-Path -LiteralPath $winhttp) { Rename-Item -LiteralPath $winhttp -NewName 'winhttp.dll.off' }
Write-Host 'Now on MelonLoader (BepInEx is off, kept as winhttp.dll.off). Back: use-bepinex.ps1' -ForegroundColor Green
